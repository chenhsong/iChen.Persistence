using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace iChen.Persistence.Server
{
	partial class OrganizationMapping : IEntityTypeConfiguration<Organization>
	{
		private string m_Schema = null;
		private ushort m_Version = 0;

		public OrganizationMapping (string schema = null, ushort version = 1)
		{
			m_Schema = schema;
			m_Version = version;
		}

		public void Configure (EntityTypeBuilder<Organization> entity)
		{
			entity.ToTable("Organizations", m_Schema);
			entity.HasKey(x => x.ID);

			entity.Property(x => x.ID).HasColumnName("ID").IsRequired();//.HasColumnType("nvarchar(100)");
			entity.Property(x => x.Name).HasColumnName("Name").IsRequired();//.HasColumnType("nvarchar(100)");
			entity.Property(x => x.TimeZoneOffset).HasColumnName("TimeZoneOffset");//.HasColumnType("real");
			entity.Property(x => x.RestrictMoldsToJobCards).HasColumnName("RestrictMoldsToJobCards").IsRequired();//.HasColumnType("bit");
			entity.Property(x => x.Created).HasColumnName("Created").IsRequired();//.HasColumnType("datetime");
			entity.Property(x => x.Modified).HasColumnName("Modified");//.HasColumnType("datetime");
		}
	}
}
