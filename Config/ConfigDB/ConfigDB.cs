using System;
using Microsoft.EntityFrameworkCore;

namespace iChen.Persistence.Server
{
	public partial class ConfigDB : DbContext, IConfigDB
	{
		public const string ConnectionName = nameof(ConfigDB);

		public static Action<DbContextOptionsBuilder> OnConnect = null;

		public const ushort Version_Organization = 100;
		public const ushort Version_TermialConfig = 100;
		public const ushort Version_Controller_LockIP = 2;
		public const ushort Version_Controller_Geo = 3;
		public const ushort Version_Controller_TimeZoneOffset = 100;
		public const ushort Version_MoldSetting_Variable = 4;

		public string Schema { get; private set; } = null;
		public ushort Version { get; private set; } = 1;

		public DbSet<Organization> Organizations { get; set; }
		public DbSet<Controller> Controllers { get; set; }
		public DbSet<Mold> Molds { get; set; }
		public DbSet<MoldSetting> MoldSettings { get; set; }
		public DbSet<User> Users { get; set; }
		public DbSet<TextMap> TextMaps { get; set; }
		public DbSet<TerminalConfig> TerminalConfigs { get; set; }

		public ConfigDB (string schema = null, ushort version = 1)
		{
			if (schema != null && string.IsNullOrWhiteSpace(schema)) throw new ArgumentOutOfRangeException(nameof(schema));

			Version = version;
			if (schema != null) Schema = schema;
		}

		protected override void OnConfiguring (DbContextOptionsBuilder optionsBuilder)
		{
			if (OnConnect == null) throw new ArgumentNullException(nameof(OnConnect));

			OnConnect(optionsBuilder);
		}

		protected override void OnModelCreating (ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.ApplyConfiguration(new ControllerMapping(Schema, Version));
			modelBuilder.ApplyConfiguration(new MoldMapping(Schema, Version));
			modelBuilder.ApplyConfiguration(new MoldSettingMapping(Schema, Version));
			modelBuilder.ApplyConfiguration(new UserMapping(Schema, Version));
			modelBuilder.ApplyConfiguration(new TextMapMapping(Schema, Version));

			if (Version >= Version_Organization) modelBuilder.ApplyConfiguration(new OrganizationMapping(Schema, Version));
			else modelBuilder.Ignore<Organization>();

			if (Version >= Version_TermialConfig) modelBuilder.ApplyConfiguration(new TerminalConfigMapping(Schema, Version));
			else modelBuilder.Ignore<TerminalConfig>();
		}
	}
}
