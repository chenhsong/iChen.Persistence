using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace iChen.Persistence.Server
{
	partial class TerminalConfigMapping : IEntityTypeConfiguration<TerminalConfig>
	{
		private string m_Schema = null;
		private ushort m_Version = 0;

		public TerminalConfigMapping (string schema = null, ushort version = 1)
		{
			m_Schema = schema;
			m_Version = version;
		}

		public void Configure (EntityTypeBuilder<TerminalConfig> entity)
		{
			entity.ToTable("TerminalConfigs", m_Schema);
			entity.HasKey(x => x.OrgId);

			entity.Property(x => x.OrgId).HasColumnName("OrgId").IsRequired();//.HasColumnType("nvarchar(100)");
			entity.Property(x => x.Text).HasColumnName("Text").IsRequired();
			entity.Property(x => x.Created).HasColumnName("Created").IsRequired();//.HasColumnType("datetime");
			entity.Property(x => x.Modified).HasColumnName("Modified");//.HasColumnType("datetime");
		}
	}
}
