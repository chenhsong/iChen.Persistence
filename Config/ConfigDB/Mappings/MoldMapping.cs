using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace iChen.Persistence.Server
{
	partial class MoldMapping : IEntityTypeConfiguration<Mold>
	{
		private string m_Schema = null;
		private ushort m_Version = 0;

		public MoldMapping (string schema = null, ushort version = 1)
		{
			m_Schema = schema;
			m_Version = version;
		}

		public void Configure (EntityTypeBuilder<Mold> entity)
		{
			entity.ToTable("Molds", m_Schema);
			entity.HasKey(x => x.ID);

			entity.Property(x => x.ID).HasColumnName("ID").IsRequired();//.HasColumnType("int").ValueGeneratedOnAdd();
			entity.Property(x => x.Name).HasColumnName("Name").IsRequired();//.HasColumnType("nvarchar(100)");
			entity.Property(x => x.ControllerId).HasColumnName("ControllerId");//.HasColumnType("int");
			entity.Property(x => x.IsEnabled).HasColumnName("IsEnabled").IsRequired();//.HasColumnType("bit");
			entity.Property(x => x.Created).HasColumnName("Created").IsRequired();//.HasColumnType("datetime");
			entity.Property(x => x.Modified).HasColumnName("Modified");//.HasColumnType("datetime");
			entity.Property(x => x.Guid).HasColumnName("GUID").IsRequired();//.HasColumnType("uniqueidentifier").ValueGeneratedOnAdd();

			entity.HasOne(a => a.Controller).WithMany(b => b.Molds).HasForeignKey(c => c.ControllerId);
		}
	}
}
