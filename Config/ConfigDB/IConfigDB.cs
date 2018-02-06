using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace iChen.Persistence.Server
{
	public interface IConfigDB : IDisposable
	{
		DbSet<Controller> Controllers { get; set; }
		DbSet<Mold> Molds { get; set; }
		DbSet<MoldSetting> MoldSettings { get; set; }
		DbSet<User> Users { get; set; }
		DbSet<TextMap> TextMaps { get; set; }
		DbSet<TerminalConfig> TerminalConfigs { get; set; }

		int SaveChanges ();
		Task<int> SaveChangesAsync (bool acceptAllChangesOnSuccess, CancellationToken cancellationToken);
	}
}
