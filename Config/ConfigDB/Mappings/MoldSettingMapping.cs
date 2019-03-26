using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace iChen.Persistence.Server
{
	internal partial class MoldSettingMapping : IEntityTypeConfiguration<MoldSetting>
	{
		private string m_Schema = null;
		private ushort m_Version = 0;

		public MoldSettingMapping (string schema = null, ushort version = 1)
		{
			m_Schema = schema;
			m_Version = version;
		}

		public void Configure (EntityTypeBuilder<MoldSetting> entity)
		{
			entity.ToTable("MoldSettings", m_Schema);
			entity.HasKey(x => new { x.MoldId, x.Offset });

			entity.Property(x => x.MoldId).HasColumnName("MoldId").IsRequired();//.HasColumnType("int");
			entity.Property(x => x.Offset).HasColumnName("Offset").IsRequired();//.HasColumnType("smallint");
			entity.Property(x => x.Value).HasColumnName("Value").IsRequired();//.HasColumnType("smallint");

			if (m_Version >= ConfigDB.Version_MoldSetting_Variable) {
				entity.Property(x => x.Variable).HasColumnName("Variable");//.HasColumnType("int");
			}

			entity.Ignore(x => x.RawData);
			entity.Property(x => x.Created).HasColumnName("Created").IsRequired();//.HasColumnType("datetime");
			entity.Property(x => x.Modified).HasColumnName("Modified");//.HasColumnType("datetime");

			entity.HasOne(a => a.Mold).WithMany(b => b.MoldSettings).IsRequired().HasForeignKey(c => c.MoldId);
		}
	}
}
