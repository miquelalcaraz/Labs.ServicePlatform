using Qlt.Domain.SeedWork;
using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;

using Testcontainers.MsSql;

namespace Platform.IntegrationTests.Persistence;

public class EntityAuditIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    private AuditTestDbContext _context = null!;

    public EntityAuditIntegrationTests()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var config = new QltDbContextConfiguration();
        config.SetDefaultSchema("audit_test");

        var options = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new AuditTestDbContext(options, config);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Insert_SetsCreatedAtToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var entity = new AuditTestEntity("test", 1);

        _context.TestEntities.Add(entity);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var persisted = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);

        persisted.CreatedAt.Should().BeOnOrAfter(before);
        persisted.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Insert_UpdatedAtIsNull()
    {
        var entity = new AuditTestEntity("test", 1);

        _context.TestEntities.Add(entity);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var persisted = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);

        persisted.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Update_SetsUpdatedAt()
    {
        var entity = new AuditTestEntity("test", 1);
        _context.TestEntities.Add(entity);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var toUpdate = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);
        var beforeUpdate = DateTimeOffset.UtcNow;

        toUpdate.SetValue(42);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var persisted = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);

        persisted.UpdatedAt.Should().NotBeNull();
        persisted.UpdatedAt!.Value.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public async Task Update_DoesNotModifyCreatedAt()
    {
        var entity = new AuditTestEntity("test", 1);
        _context.TestEntities.Add(entity);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var toUpdate = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);
        var originalCreatedAt = toUpdate.CreatedAt;

        toUpdate.SetValue(99);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var persisted = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);

        persisted.CreatedAt.Should().Be(originalCreatedAt);
    }

    [Fact]
    public async Task Insert_SetsRowVersion()
    {
        var entity = new AuditTestEntity("test", 1);
        _context.TestEntities.Add(entity);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var persisted = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);

        persisted.RowVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Update_ChangesRowVersion()
    {
        var entity = new AuditTestEntity("test", 1);
        _context.TestEntities.Add(entity);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var toUpdate = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);
        var originalRowVersion = toUpdate.RowVersion.ToArray();

        toUpdate.SetValue(42);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var persisted = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);

        persisted.RowVersion.Should().NotBeEquivalentTo(originalRowVersion);
    }

    [Fact]
    public async Task ConcurrentUpdate_WithStaleRowVersion_ThrowsConcurrencyException()
    {
        var entity = new AuditTestEntity("test", 1);
        _context.TestEntities.Add(entity);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var config = new QltDbContextConfiguration();
        config.SetDefaultSchema("audit_test");

        var options2 = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        await using var context2 = new AuditTestDbContext(options2, config);

        var entity1 = await _context.TestEntities.SingleAsync(e => e.Id == entity.Id);
        var entity2 = await context2.TestEntities.SingleAsync(e => e.Id == entity.Id);

        entity1.SetValue(10);
        await _context.SaveChangesAsync();

        entity2.SetValue(20);
        var act = () => context2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    #region Test Infrastructure

    public class AuditTestEntity : Entity, IAggregateRoot
    {
        public string Name { get; private set; } = null!;
        public int Value { get; private set; }

        private AuditTestEntity() { }

        public AuditTestEntity(string name, int value)
        {
            Name = name;
            Value = value;
        }

        public void SetValue(int newValue) => Value = newValue;
    }

    public class AuditTestDbContext : QltDBContext
    {
        public DbSet<AuditTestEntity> TestEntities => Set<AuditTestEntity>();

        public AuditTestDbContext(
            DbContextOptions<AuditTestDbContext> options,
            QltDbContextConfiguration configuration)
            : base(options, configuration) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AuditTestEntity>(entity =>
            {
                entity.ToTable("AuditTestEntities");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            });
        }
    }

    #endregion
}
