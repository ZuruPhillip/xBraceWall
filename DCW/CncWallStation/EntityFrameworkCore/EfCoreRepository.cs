using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace CncWallStation.EntityFrameworkCore
{
    /// <summary>
    /// 基于 EF Core 的简易 IRepository 实现（不依赖完整 ABP 框架）
    /// </summary>
    public class EfCoreRepository<TEntity, TKey> : IRepository<TEntity, TKey>
        where TEntity : class, IEntity<TKey>
    {
        private readonly AppDbContext _db;
        protected DbSet<TEntity> DbSet => _db.Set<TEntity>();

        public IAsyncQueryableExecuter AsyncExecuter => throw new NotImplementedException();

        public bool? IsChangeTrackingEnabled => throw new NotImplementedException();

        public EfCoreRepository(AppDbContext db)
        {
            _db = db;
        }

        // ========== Queryable ==========
        public Task<IQueryable<TEntity>> GetQueryableAsync()
            => Task.FromResult<IQueryable<TEntity>>(DbSet);

        public IQueryable<TEntity> WithDetails() => DbSet;
        public IQueryable<TEntity> WithDetails(params Expression<Func<TEntity, object>>[] propertySelectors)
        {
            IQueryable<TEntity> q = DbSet;
            foreach (var s in propertySelectors) q = q.Include(s);
            return q;
        }

        // ========== Get ==========
        public async Task<TEntity> GetAsync(TKey id, bool includeDetails = true, CancellationToken ct = default)
        {
            var entity = await FindAsync(id, includeDetails, ct);
            if (entity == null)
                throw new EntityNotFoundException(typeof(TEntity), id!);
            return entity;
        }

        public Task<TEntity?> FindAsync(TKey id, bool includeDetails = true, CancellationToken ct = default)
        {
            return DbSet.FirstOrDefaultAsync(
                e => EF.Property<TKey>(e, "Id")!.Equals(id), ct);
        }

        public async Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate,
            bool includeDetails = true, CancellationToken ct = default)
        {
            var entity = await DbSet.FirstOrDefaultAsync(predicate, ct);
            if (entity == null) throw new EntityNotFoundException(typeof(TEntity));
            return entity;
        }

        public Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate,
            bool includeDetails = true, CancellationToken ct = default)
            => DbSet.FirstOrDefaultAsync(predicate, ct);

        // ========== List ==========
        public async Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken ct = default)
            => await DbSet.ToListAsync(ct);

        public async Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate,
            bool includeDetails = false, CancellationToken ct = default)
            => await DbSet.Where(predicate).ToListAsync(ct);

        public Task<long> GetCountAsync(CancellationToken ct = default)
            => DbSet.LongCountAsync(ct);

        public async Task<List<TEntity>> GetPagedListAsync(int skipCount, int maxResultCount,
            string sorting, bool includeDetails = false, CancellationToken ct = default)
            => await DbSet.Skip(skipCount).Take(maxResultCount).ToListAsync(ct);

        // ========== Insert ==========
        public async Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken ct = default)
        {
            await DbSet.AddAsync(entity, ct);
            if (autoSave) await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken ct = default)
        {
            await DbSet.AddRangeAsync(entities, ct);
            if (autoSave) await _db.SaveChangesAsync(ct);
        }

        // ========== Update ==========
        public async Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken ct = default)
        {
            DbSet.Update(entity);
            if (autoSave) await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken ct = default)
        {
            DbSet.UpdateRange(entities);
            if (autoSave) await _db.SaveChangesAsync(ct);
        }

        // ========== Delete ==========
        public async Task DeleteAsync(TKey id, bool autoSave = false, CancellationToken ct = default)
        {
            var entity = await FindAsync(id, true, ct);
            if (entity != null)
            {
                DbSet.Remove(entity);
                if (autoSave) await _db.SaveChangesAsync(ct);
            }
        }

        public async Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken ct = default)
        {
            DbSet.Remove(entity);
            if (autoSave) await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Expression<Func<TEntity, bool>> predicate,
            bool autoSave = false, CancellationToken ct = default)
        {
            var entities = await DbSet.Where(predicate).ToListAsync(ct);
            DbSet.RemoveRange(entities);
            if (autoSave) await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteManyAsync(IEnumerable<TKey> ids, bool autoSave = false, CancellationToken ct = default)
        {
            var entities = await DbSet.Where(
                e => ids.Contains(EF.Property<TKey>(e, "Id"))).ToListAsync(ct);
            DbSet.RemoveRange(entities);
            if (autoSave) await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken ct = default)
        {
            DbSet.RemoveRange(entities);
            if (autoSave) await _db.SaveChangesAsync(ct);
        }

        public Task DeleteDirectAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IQueryable<TEntity>> WithDetailsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IQueryable<TEntity>> WithDetailsAsync(params Expression<Func<TEntity, object>>[] propertySelectors)
        {
            throw new NotImplementedException();
        }

        // ========== 其余接口成员根据需要补全 ==========
    }
}