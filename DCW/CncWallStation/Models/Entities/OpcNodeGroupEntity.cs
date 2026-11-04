using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// OPC 节点分组实体 —— 用于对 OPC 节点进行分类管理
    /// </summary>
    [Table("OpcNodeGroup")]
    public class OpcNodeGroupEntity : Entity<int>
    {
        // ==================== 构造函数 ====================

        /// <summary>EF Core 专用无参构造函数</summary>
        protected OpcNodeGroupEntity() { }

        public OpcNodeGroupEntity(string name, string? description, int sortOrder)
        {
            Name = name;
            Description = description;
            SortOrder = sortOrder;
            CreatedAt = DateTime.Now;
        }

        // ==================== 属性 ====================

        /// <summary>分组名称（唯一）</summary>
        [Required]
        [MaxLength(64)]
        public string Name { get; private set; } = string.Empty;

        /// <summary>分组描述</summary>
        [MaxLength(256)]
        public string? Description { get; private set; }

        /// <summary>排序序号（升序显示）</summary>
        public int SortOrder { get; private set; }

        /// <summary>创建时间</summary>
        [Required]
        public DateTime CreatedAt { get; private set; } = DateTime.Now;

        // ==================== 领域方法 ====================

        /// <summary>重命名并更新描述</summary>
        public void Update(string name, string? description)
        {
            Name = name;
            Description = description;
        }

        public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
    }
}
