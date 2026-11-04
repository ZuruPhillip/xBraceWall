using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// OPC 节点配置实体 —— 持久化需要读写/订阅的 OPC UA 节点
    /// </summary>
    [Table("OpcNode")]
    public class OpcNodeEntity : Entity<long>
    {
        // ==================== 构造函数 ====================

        /// <summary>EF Core 专用无参构造函数</summary>
        protected OpcNodeEntity() { }

        public OpcNodeEntity(
            string nodeId,
            string? description,
            int? groupId,
            bool isWritable,
            bool isShowInRealtime,
            int sortOrder)
        {
            NodeId = nodeId;
            Description = description;
            GroupId = groupId;
            IsWritable = isWritable;
            IsShowInRealtime = isShowInRealtime;
            SortOrder = sortOrder;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        // ==================== 属性 ====================

        /// <summary>所属分组（null 表示未分组）</summary>
        public int? GroupId { get; private set; }

        /// <summary>节点唯一标识，如 ns=3;s="MyVariable"</summary>
        [Required]
        [MaxLength(256)]
        public string NodeId { get; private set; } = string.Empty;

        /// <summary>节点描述/别名</summary>
        [MaxLength(256)]
        public string? Description { get; private set; }

        /// <summary>是否可写</summary>
        public bool IsWritable { get; private set; }

        /// <summary>是否在控制页面“实时参数”面板中显示</summary>
        public bool IsShowInRealtime { get; private set; }

        /// <summary>排序序号（升序显示）</summary>
        public int SortOrder { get; private set; }

        /// <summary>创建时间</summary>
        [Required]
        public DateTime CreatedAt { get; private set; } = DateTime.Now;

        /// <summary>最后更新时间</summary>
        [Required]
        public DateTime UpdatedAt { get; private set; } = DateTime.Now;

        // ==================== 领域方法 ====================

        /// <summary>更新节点可编辑属性（NodeId 作为订阅键不允许修改）</summary>
        public void Update(string? description, int? groupId, bool isWritable, bool isShowInRealtime)
        {
            Description = description;
            GroupId = groupId;
            IsWritable = isWritable;
            IsShowInRealtime = isShowInRealtime;
            UpdatedAt = DateTime.Now;
        }

        /// <summary>移动到指定分组（分组被删除时移到未分组）</summary>
        public void MoveToGroup(int? groupId)
        {
            GroupId = groupId;
            UpdatedAt = DateTime.Now;
        }

        public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
    }
}
