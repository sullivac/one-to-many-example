using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace OneToManyExample;

public class OneToManyTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=postgres;Database=OneToManyExample;Username=postgres;Password=postgres";

    private readonly DbContextOptions<OneToManyDbContext> _options;

    public OneToManyTests()
    {
        _options =
            new DbContextOptionsBuilder<OneToManyDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
    }

    [Fact]
    public async Task OneToMany_WhenChildIdDatabaseGeneratedNone_SavesChanges()
    {
        var parentId = Guid.NewGuid();
        var firstChildId = Guid.NewGuid();
        var secondChildId = Guid.NewGuid();

        await AddParent(parentId);
        await AddChild(parentId, firstChildId, "Child1");
        await AddChild(parentId, secondChildId, "Child2");

        using var unitOfWork = new OneToManyDbContext(_options);

        var result =
            await unitOfWork.Parents
                .Include(parent => parent.Children)
                .FirstOrDefaultAsync(parent => parent.Id == parentId);

        result.Should().BeEquivalentTo(
            new
            {
                Id = parentId,
                Name = "Parent",
                Children =
                    new[]
                    {
                        new
                        {
                            Id = firstChildId,
                            Name = "Child1",
                            ParentId = parentId
                        },
                        new
                        {
                            Id = secondChildId,
                            Name = "Child2",
                            ParentId = parentId
                        }
                    }
            });
    }

    [Fact]
    public async Task
        OneToMany_WhenChildIdDatabaseGeneratedDefault_ThrowsException()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        await AddBrokenParent(parentId);

        using var unitOfWork = new OneToManyDbContext(_options);

        var parent =
            await unitOfWork.BrokenParents.FindAsync(parentId)
            ?? throw new InvalidOperationException("Parent does not exist.");

        parent.Children.Add(
            new BrokenChild
            {
                Id = childId,
                Name = "Child",
                Parent = parent
            });

        await unitOfWork.Invoking(
            async unitOfWork => await unitOfWork.SaveChangesAsync())
            .Should()
            .ThrowAsync<DbUpdateException>()
            .WithMessage(
                "The database operation was expected to affect 1 row(s), but " +
                "actually affected 0 row(s); data may have been modified or " +
                "deleted since entities were loaded. See " +
                "https://go.microsoft.com/fwlink/?LinkId=527962 for " +
                "information on understanding and handling optimistic " +
                "concurrency exceptions.");
    }

    public async Task InitializeAsync()
    {
        using var context = new OneToManyDbContext(_options);

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private async Task AddParent(Guid parentId)
    {
        using var unitOfWork = new OneToManyDbContext(_options);

        var parent =
            new Parent
            {
                Id = parentId,
                Name = "Parent"
            };

        unitOfWork.Parents.Add(parent);

        await unitOfWork.SaveChangesAsync();
    }

    private async Task AddChild(Guid parentId, Guid childId, string childName)
    {
        using var unitOfWork = new OneToManyDbContext(_options);

        var parent =
            await unitOfWork.Parents.FindAsync(parentId)
            ?? throw new InvalidOperationException("Parent does not exist.");

        parent.Children.Add(
            new Child
            {
                Id = childId,
                Name = childName,
                Parent = parent
            });

        await unitOfWork.SaveChangesAsync();
    }

    private async Task AddBrokenParent(Guid parentId)
    {
        using var unitOfWork = new OneToManyDbContext(_options);

        var parent =
            new BrokenParent
            {
                Id = parentId,
                Name = "Parent"
            };

        unitOfWork.BrokenParents.Add(parent);

        await unitOfWork.SaveChangesAsync();
    }
}

public class OneToManyDbContext(DbContextOptions<OneToManyDbContext> options)
    : DbContext(options)
{
    public DbSet<BrokenParent> BrokenParents { get; set; } = null!;

    public DbSet<Parent> Parents { get; set; } = null!;
}

public class Parent
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<Child> Children { get; set; } = [];
}

public class Child
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid ParentId { get; set; }

    public Parent Parent { get; set; } = null!;
}

public class BrokenOneToManyDbContext(
    DbContextOptions<OneToManyDbContext> options)
    : DbContext(options)
{
    public DbSet<Parent> Parents { get; set; } = null!;
}

public class BrokenParent
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<BrokenChild> Children { get; set; } = [];
}

public class BrokenChild
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid BrokenParentId { get; set; }

    public BrokenParent Parent { get; set; } = null!;
}
