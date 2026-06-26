using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// OPC 写入记录实体 —— 记录每次下发给 PLC 的节点数据，通过 GroupId 区分批次
    /// </summary>
    [Table("Opc")]
    public class OpcWriteRecordEntity : Entity<long>
    {
        /// <summary>关联墙体数据库主键</summary>
        public long WallId { get; set; }

        /// <summary>批次标识，同一批次写入共用同一个 GUID</summary>
        public string GroupId { get; set; } = string.Empty;

        /// <summary>OPC UA 节点 ID（如 ns=2;s=unit/MCCUnit_35.InDATA_CNC_P.LineDef[0].T）</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>写入值（以字符串形式存储，兼容 int / float 等类型）</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>记录创建时间</summary>
        public DateTime CreatedAt { get; set; }

        protected OpcWriteRecordEntity() { }

        public OpcWriteRecordEntity(long wallId, string groupId, string nodeId, string value)
        {
            WallId = wallId;
            GroupId = groupId;
            NodeId = nodeId;
            Value = value;
            CreatedAt = DateTime.Now;
        }
    }
}
