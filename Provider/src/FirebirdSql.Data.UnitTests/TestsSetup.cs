﻿/*
 *	Firebird ADO.NET Data provider for .NET	and	Mono
 *
 *	   The contents	of this	file are subject to	the	Initial
 *	   Developer's Public License Version 1.0 (the "License");
 *	   you may not use this	file except	in compliance with the
 *	   License.	You	may	obtain a copy of the License at
 *	   http://www.firebirdsql.org/index.php?op=doc&id=idpl
 *
 *	   Software	distributed	under the License is distributed on
 *	   an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *	   express or implied.	See	the	License	for	the	specific
 *	   language	governing rights and limitations under the License.
 *
 *	Copyright (c) 2015 Jiri Cincura (jiri@cincura.net)
 *	All	Rights Reserved.
 */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebirdSql.Data.FirebirdClient;
using NUnit.Framework;

namespace FirebirdSql.Data.UnitTests
{
	[SetUpFixture]
	public class TestsSetup
	{
		internal const string UserID = "SYSDBA";
		internal const string Password = "masterkey";
		internal const string Database = "netprovider_tests.fdb";
		internal const string DataSource = "localhost";
		internal const int Port = 3050;
		internal const string Charset = "utf8";
		internal const bool Pooling = false;
		internal const int PageSize = 16384;
		internal const bool ForcedWrite = false;
		internal const string BackupRestoreFile = "netprovider_tests.fbk";

		private static HashSet<FbServerType> _initalized = new HashSet<FbServerType>();

		public static void SetUp(FbServerType serverType)
		{
			if (!_initalized.Contains(serverType))
			{
				Prepare(serverType);
				_initalized.Add(serverType);
			}
		}

		[OneTimeTearDown]
		public void TearDown()
		{
			FbConnection.ClearAllPools();
			foreach (var item in _initalized)
			{
				Drop(item);
			}
			_initalized.Clear();
		}

		private static void Drop(FbServerType serverType)
		{
			string cs = TestsBase.BuildConnectionString(serverType);
			DropDatabase(cs);
		}

		private static void Prepare(FbServerType serverType)
		{
			string cs = TestsBase.BuildConnectionString(serverType);
			CreateDatabase(cs);
			CreateTables(cs);
			CreateProcedures(cs);
			CreateTriggers(cs);
		}

		private static void CreateDatabase(string connectionString)
		{
			FbConnection.CreateDatabase(connectionString, PageSize, ForcedWrite, true);
		}

		private static void DropDatabase(string connectionString)
		{
			FbConnection.DropDatabase(connectionString);
		}

		private static void CreateTables(string connectionString)
		{
			FbConnection connection = new FbConnection(connectionString);
			connection.Open();

			StringBuilder commandText = new StringBuilder();

			// Table for general purpouse tests
			commandText.Append("RECREATE TABLE TEST (");
			commandText.Append("INT_FIELD		 INTEGER DEFAULT 0 NOT NULL	PRIMARY	KEY,");
			commandText.Append("CHAR_FIELD		 CHAR(30),");
			commandText.Append("VARCHAR_FIELD	 VARCHAR(100),");
			commandText.Append("BIGINT_FIELD	 BIGINT,");
			commandText.Append("SMALLINT_FIELD	 SMALLINT,");
			commandText.Append("DOUBLE_FIELD	 DOUBLE	PRECISION,");
			commandText.Append("FLOAT_FIELD		 FLOAT,");
			commandText.Append("NUMERIC_FIELD	 NUMERIC(15,2),");
			commandText.Append("DECIMAL_FIELD	 DECIMAL(15,2),");
			commandText.Append("DATE_FIELD		 DATE,");
			commandText.Append("TIME_FIELD		 TIME,");
			commandText.Append("TIMESTAMP_FIELD	 TIMESTAMP,");
			commandText.Append("CLOB_FIELD		 BLOB SUB_TYPE 1 SEGMENT SIZE 80,");
			commandText.Append("BLOB_FIELD		 BLOB SUB_TYPE 0 SEGMENT SIZE 80,");
			commandText.Append("IARRAY_FIELD	 INTEGER [0:3],");
			commandText.Append("SARRAY_FIELD	 SMALLINT [0:4],");
			commandText.Append("LARRAY_FIELD	 BIGINT	[0:5],");
			commandText.Append("FARRAY_FIELD	 FLOAT [0:3],");
			commandText.Append("BARRAY_FIELD	 DOUBLE	PRECISION [1:4],");
			commandText.Append("NARRAY_FIELD	 NUMERIC(10,6) [1:4],");
			commandText.Append("DARRAY_FIELD	 DATE [1:4],");
			commandText.Append("TARRAY_FIELD	 TIME [1:4],");
			commandText.Append("TSARRAY_FIELD	 TIMESTAMP [1:4],");
			commandText.Append("CARRAY_FIELD	 CHAR(21) [1:4],");
			commandText.Append("VARRAY_FIELD	 VARCHAR(30) [1:4],");
			commandText.Append("BIG_ARRAY		 INTEGER [1:32767],");
			commandText.Append("EXPR_FIELD		 COMPUTED BY (smallint_field * 1000),");
			commandText.Append("CS_FIELD		 CHAR(1) CHARACTER SET UNICODE_FSS,");
			commandText.Append("UCCHAR_ARRAY	 CHAR(10) [1:10] CHARACTER SET UNICODE_FSS);");

			FbCommand command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			command = new FbCommand("recreate table PrepareTest(test_field varchar(20));", connection);
			command.ExecuteNonQuery();
			command.Dispose();

			command = new FbCommand("recreate table log(occured timestamp, text varchar(20));", connection);
			command.ExecuteNonQuery();
			command.Dispose();

			FbCommand createTable = new FbCommand("RECREATE TABLE GUID_TEST (INT_FIELD INTEGER, GUID_FIELD CHAR(16) CHARACTER SET OCTETS)", connection);
			createTable.ExecuteNonQuery();
			createTable.Dispose();

			connection.Close();
		}

		private static void CreateProcedures(string connectionString)
		{
			FbConnection connection = new FbConnection(connectionString);
			connection.Open();

			StringBuilder commandText = new StringBuilder();

			commandText = new StringBuilder();

			commandText.Append("RECREATE PROCEDURE SELECT_DATA  \r\n");
			commandText.Append("RETURNS	( \r\n");
			commandText.Append("INT_FIELD INTEGER, \r\n");
			commandText.Append("VARCHAR_FIELD VARCHAR(100),	\r\n");
			commandText.Append("DECIMAL_FIELD DECIMAL(15,2)) \r\n");
			commandText.Append("AS \r\n");
			commandText.Append("begin \r\n");
			commandText.Append("FOR	SELECT INT_FIELD, VARCHAR_FIELD, DECIMAL_FIELD FROM	TEST INTO :INT_FIELD, :VARCHAR_FIELD, :DECIMAL_FIELD \r\n");
			commandText.Append("DO \r\n");
			commandText.Append("SUSPEND; \r\n");
			commandText.Append("end;");

			FbCommand command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			commandText = new StringBuilder();

			commandText.Append("RECREATE PROCEDURE GETRECORDCOUNT	\r\n");
			commandText.Append("RETURNS	( \r\n");
			commandText.Append("RECCOUNT SMALLINT) \r\n");
			commandText.Append("AS \r\n");
			commandText.Append("begin \r\n");
			commandText.Append("for	select count(*)	from test into :reccount \r\n");
			commandText.Append("do \r\n");
			commandText.Append("suspend; \r\n");
			commandText.Append("end\r\n");

			command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			commandText = new StringBuilder();

			commandText.Append("RECREATE PROCEDURE GETVARCHARFIELD (\r\n");
			commandText.Append("ID INTEGER)\r\n");
			commandText.Append("RETURNS	(\r\n");
			commandText.Append("VARCHAR_FIELD VARCHAR(100))\r\n");
			commandText.Append("AS\r\n");
			commandText.Append("begin\r\n");
			commandText.Append("for	select varchar_field from test where int_field = :id into :varchar_field\r\n");
			commandText.Append("do\r\n");
			commandText.Append("suspend;\r\n");
			commandText.Append("end\r\n");

			command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			commandText = new StringBuilder();

			commandText.Append("RECREATE PROCEDURE GETASCIIBLOB (\r\n");
			commandText.Append("ID INTEGER)\r\n");
			commandText.Append("RETURNS	(\r\n");
			commandText.Append("ASCII_BLOB BLOB	SUB_TYPE 1)\r\n");
			commandText.Append("AS\r\n");
			commandText.Append("begin\r\n");
			commandText.Append("for	select clob_field from test	where int_field	= :id into :ascii_blob\r\n");
			commandText.Append("do\r\n");
			commandText.Append("suspend;\r\n");
			commandText.Append("end\r\n");

			command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			commandText = new StringBuilder();

			commandText.Append("RECREATE PROCEDURE DATAREADERTEST\r\n");
			commandText.Append("RETURNS	(\r\n");
			commandText.Append("content	VARCHAR(128))\r\n");
			commandText.Append("AS\r\n");
			commandText.Append("begin\r\n");
			commandText.Append("content	= 'test';\r\n");
			commandText.Append("suspend;\r\n");
			commandText.Append("end\r\n");

			command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			commandText = new StringBuilder();

			commandText.Append("recreate procedure SimpleSP\r\n");
			commandText.Append("returns ( result integer ) as\r\n");
			commandText.Append("begin\r\n");
			commandText.Append("result = 1000;\r\n");
			commandText.Append("end \r\n");

			command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			connection.Close();
		}

		private static void CreateTriggers(string connectionString)
		{
			FbConnection connection = new FbConnection(connectionString);
			connection.Open();

			StringBuilder commandText = new StringBuilder();

			commandText = new StringBuilder();

			commandText.Append("RECREATE TRIGGER new_row FOR test	ACTIVE\r\n");
			commandText.Append("AFTER INSERT POSITION 0\r\n");
			commandText.Append("AS\r\n");
			commandText.Append("BEGIN\r\n");
			commandText.Append("POST_EVENT 'new	row';\r\n");
			commandText.Append("END");

			FbCommand command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			commandText = new StringBuilder();

			commandText.Append("RECREATE TRIGGER update_row FOR test ACTIVE\r\n");
			commandText.Append("AFTER UPDATE POSITION 0\r\n");
			commandText.Append("AS\r\n");
			commandText.Append("BEGIN\r\n");
			commandText.Append("POST_EVENT 'updated	row';\r\n");
			commandText.Append("END");

			command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			commandText = new StringBuilder();

			commandText.Append("recreate trigger log active on connect\r\n");
			commandText.Append("as\r\n");
			commandText.Append("begin\r\n");
			commandText.Append("insert into log (occured, text) values (current_timestamp, 'on connect');\r\n");
			commandText.Append("end");

			command = new FbCommand(commandText.ToString(), connection);
			command.ExecuteNonQuery();
			command.Dispose();

			connection.Close();
		}
	}
}
