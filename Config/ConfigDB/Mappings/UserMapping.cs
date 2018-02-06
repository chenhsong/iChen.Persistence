using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace iChen.Persistence.Server
{
	partial class UserMapping : IEntityTypeConfiguration<User>
	{
		private string m_Schema = null;
		private ushort m_Version = 0;

		public UserMapping (string schema = null, ushort version = 1)
		{
			m_Schema = schema;
			m_Version = version;
		}

		public void Configure (EntityTypeBuilder<User> entity)
		{
			entity.ToTable("Users", m_Schema);
			entity.HasKey(x => x.ID);

			entity.Property(x => x.ID).HasColumnName("ID").IsRequired().HasColumnType("int").ValueGeneratedOnAdd();
			entity.Property(x => x.OrgId).HasColumnName("OrgId").IsRequired().HasColumnType("nvarchar(100)");
			entity.Property(x => x.Password).HasColumnName("Password").IsRequired().HasColumnType("nvarchar(50)");
			entity.Property(x => x.Name).HasColumnName("Name").IsRequired().HasColumnType("nvarchar(50)");
			entity.Property(x => x.IsEnabled).HasColumnName("IsEnabled").IsRequired().HasColumnType("bit");
			entity.Property(x => x.Filters).HasColumnName("Filters").IsRequired().HasColumnType("int");
			entity.Property(x => x.AccessLevel).HasColumnName("AccessLevel").IsRequired().HasColumnType("tinyint");
			entity.Property(x => x.Created).HasColumnName("Created").IsRequired().HasColumnType("datetime");
			entity.Property(x => x.Modified).HasColumnName("Modified").HasColumnType("datetime");
		}
	}
}
