using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace iChen.Persistence.Server
{
	partial class ControllerMapping : IEntityTypeConfiguration<Controller>
	{
		private string m_Schema = null;
		private ushort m_Version = 0;

		public ControllerMapping (string schema = null, ushort version = 1)
		{
			m_Schema = schema;
			m_Version = version;
		}

		public void Configure (EntityTypeBuilder<Controller> entity)
		{
			entity.ToTable("Controllers", m_Schema);
			entity.HasKey(x => x.ID);

			entity.Property(x => x.ID).HasColumnName("ID").IsRequired().HasColumnType("int");
			entity.Property(x => x.OrgId).HasColumnName("OrgId").IsRequired().HasColumnType("nvarchar(100)");
			entity.Property(x => x.IsEnabled).HasColumnName("IsEnabled").IsRequired().HasColumnType("bit");
			entity.Property(x => x.Name).HasColumnName("Name").IsRequired().HasColumnType("nvarchar(100)");
			entity.Property(x => x.Type).HasColumnName("Type").IsRequired().HasColumnType("int");
			entity.Property(x => x.Version).HasColumnName("Version").IsRequired().HasColumnType("nvarchar(50)");
			entity.Property(x => x.Model).HasColumnName("Model").IsRequired().HasColumnType("nvarchar(100)");
			entity.Property(x => x.IP).HasColumnName("IP").IsRequired().HasColumnType("nvarchar(25)");
			entity.Property(x => x.Created).HasColumnName("Created").IsRequired().HasColumnType("datetime");
			entity.Property(x => x.Modified).HasColumnName("Modified").HasColumnType("datetime");

			// LockIP field was added in version 2
			if (m_Version < ConfigDB.Version_Controller_LockIP) {
				entity.Ignore(x => x.LockIP);
			} else {
				entity.Property(x => x.LockIP).HasColumnName("LockIP").HasColumnType("nvarchar(100)");
			}

			// LockIP field was added in version 3
			if (m_Version < ConfigDB.Version_Controller_Geo) {
				entity.Ignore(x => x.GeoLatitude);
				entity.Ignore(x => x.GeoLongitude);
			} else {
				entity.Property(x => x.GeoLatitude).HasColumnName("GeoLatitude");
				entity.Property(x => x.GeoLatitude).HasColumnName("GeoLongitude");
			}

			// TimeZoneOffset field was added in version 100
			if (m_Version < ConfigDB.Version_Controller_TimeZoneOffset) {
				entity.Ignore(x => x.TimeZoneOffset);
			} else {
				entity.Property(x => x.TimeZoneOffset).HasColumnName("TimeZoneOffset").HasColumnType("real");
			}
		}
	}
}