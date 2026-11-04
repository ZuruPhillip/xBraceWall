using CommunityToolkit.Mvvm.ComponentModel;

namespace CncWallStation.ViewModels
{
    /// <summary>
    /// 左侧分组栏 / 分组下拉的条目。
    /// GroupId 为 null 时表示伪条目：“全部节点”（IsAll=true）或“未分组”（IsAll=false）。
    /// </summary>
    public partial class OpcNodeGroupItem : ObservableObject
    {
        public OpcNodeGroupItem(int? groupId, bool isAll, string name, int nodeCount = 0)
        {
            GroupId = groupId;
            IsAll = isAll;
            Name = name;
            NodeCount = nodeCount;
        }

        /// <summary>分组主键（伪条目为 null）</summary>
        public int? GroupId { get; }

        /// <summary>是否为“全部节点”伪条目</summary>
        public bool IsAll { get; }

        /// <summary>是否为真实的数据库分组（可重命名/删除）</summary>
        public bool IsManageable => GroupId.HasValue;

        /// <summary>是否为“未分组”伪条目</summary>
        public bool IsUngrouped => GroupId == null && !IsAll;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private int _nodeCount;
    }
}
