namespace CncWallStation.Models.Dtos
{
    /// <summary>
    /// ABP 风格分页结果
    /// </summary>
    public class PagedResultDto<T>
    {
        /// <summary>总记录数</summary>
        public long TotalCount { get; set; }

        /// <summary>当前页数据</summary>
        public List<T> Items { get; set; } = new();
    }
}
