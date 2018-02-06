using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace iChen.Persistence.Server
{
	partial class TextMapMapping : IEntityTypeConfiguration<TextMap>
	{
		private string m_Schema = null;
		private ushort m_Version = 0;

		public TextMapMapping (string schema = null, ushort version = 1)
		{
			m_Schema = schema;
			m_Version = version;
		}

		public void Configure (EntityTypeBuilder<TextMap> entity)
		{
			entity.ToTable("TextMaps", m_Schema);
			entity.HasKey(x => x.ID);

			entity.Property(x => x.ID).HasColumnName("ID").IsRequired().HasColumnType("int").ValueGeneratedOnAdd();
			entity.Property(x => x.Text).HasColumnName("Text").IsRequired().HasColumnType("nvarchar(255)");
		}
	}
}