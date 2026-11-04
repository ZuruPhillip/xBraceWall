namespace CncWallStation.ViewModels
{
    /// <summary>
    /// 分页条上的页码项（页码或省略号）
    /// </summary>
    public class PageNumberItem
    {
        /// <summary>显示文本</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>目标页码（省略号为 0）</summary>
        public int PageIndex { get; init; }

        /// <summary>是否为当前页</summary>
        public bool IsCurrent { get; init; }

        /// <summary>是否为省略号占位</summary>
        public bool IsEllipsis { get; init; }
    }
}
