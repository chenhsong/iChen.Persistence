using System;
using System.Data.Odbc;
using Microsoft.WindowsAzure.Storage.Table;

namespace iChen.Analytics
{
	public interface IPredicate<T>
	{
		Func<DynamicTableEntity, bool> GetLinqFilter ();

		string GetSqlWhereClause ();

		void AddSqlParameters (OdbcParameterCollection parameters);
	}
}