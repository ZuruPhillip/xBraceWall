using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// PLC 指令实体 —— 独立存储每条 PLC 指令，通过 WallId 关联墙体
    /// </summary>
    [Table("PlcInstruction")]
    public class PlcInstructionEntity : Entity<long>
    {
        /// <summary>关联墙体 Id（外键 → WallEntity.Id）</summary>
        public long WallId { get; set; }

        /// <summary>刀具编号（T 值）</summary>
        public int T { get; set; }

        /// <summary>特征子类型（F 值）</summary>
        public int F { get; set; }

        /// <summary>重复数量（D 值）</summary>
        public int D { get; set; }

        /// <summary>起点 X 坐标</summary>
        public float X0 { get; set; }

        /// <summary>起点 Y 坐标</summary>
        public float Y0 { get; set; }

        /// <summary>起点 Z 坐标</summary>
        public float Z0 { get; set; }

        /// <summary>终点 X 坐标</summary>
        public float X1 { get; set; }

        /// <summary>终点 Y 坐标</summary>
        public float Y1 { get; set; }

        /// <summary>终点 Z 坐标</summary>
        public float Z1 { get; set; }

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }

        /// <summary>Handler 名称（如 "WallHandler"）</summary>
        public string HandlerName { get; set; } = string.Empty;

        /// <summary>特征中文名称（如 "墙定义"）</summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>最后更新人</summary>
        public string? UpdatedBy { get; set; }

        /// <summary>最后更新时间</summary>
        public DateTime UpdatedAt { get; set; }

        // ==================== 导航属性 ====================
        public WallEntity Wall { get; set; } = null!;
    }
}
