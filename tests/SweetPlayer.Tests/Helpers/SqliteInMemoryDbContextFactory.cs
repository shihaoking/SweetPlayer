using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SweetPlayer.Core.Data;

namespace SweetPlayer.Tests.Helpers;

/// <summary>
/// 为集成测试创建基于内存 SQLite 的 <see cref="IDbContextFactory{TContext}"/>，
/// 通过共享 <see cref="SqliteConnection"/> 保证多次创建上下文时数据可见。
/// </summary>
internal sealed class SqliteInMemoryDbContextFactory : IDbContextFactory<SweetPlayerDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SweetPlayerDbContext> _options;

    public SqliteInMemoryDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<SweetPlayerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new SweetPlayerDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public SweetPlayerDbContext CreateDbContext() => new(_options);

    public void Dispose()
    {
        _connection.Dispose();
    }
}
