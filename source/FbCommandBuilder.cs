/*
 *  Firebird ADO.NET Data provider for .NET and Mono 
 * 
 *     The contents of this file are subject to the Initial 
 *     Developer's Public License Version 1.0 (the "License"); 
 *     you may not use this file except in compliance with the 
 *     License. You may obtain a copy of the License at 
 *     http://www.ibphoenix.com/main.nfs?a=ibphoenix&l=;PAGES;NAME='ibp_idpl'
 *
 *     Software distributed under the License is distributed on 
 *     an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either 
 *     express or implied.  See the License for the specific 
 *     language governing rights and limitations under the License.
 * 
 *  Copyright (c) 2002, 2004 Carlos Guzman Alvarez
 *  All Rights Reserved.
 */

using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.ComponentModel;

using FirebirdSql.Data.INGDS;
using FirebirdSql.Data.NGDS;

namespace FirebirdSql.Data.Firebird
{
	/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="T:FbCommandBuilder"]/*'/>
	public sealed class FbCommandBuilder : Component
	{
		#region FIELDS

		private FbDataAdapter	dataAdapter;
		private string		sqlInsert;
		private string		sqlUpdate;
		private string		sqlDelete;
		private string		whereClausule;
		private string		setClausule;
		private DataTable	schemaTable;
		private FbCommand	insertCommand;
		private FbCommand	updateCommand;
		private FbCommand	deleteCommand;		
		private string		quotePrefix;
		private string		quoteSuffix;
		private bool		disposed;
		private string		tableName;
		private bool		hasPrimaryKey;

		#endregion

		#region PROPERTIES

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="P:DataAdapter"]/*'/>
		public FbDataAdapter DataAdapter
		{
			get { return dataAdapter; }
			set
			{			
				dataAdapter	= value;

				// Registers the CommandBuilder as a listener for RowUpdating events that are 
				// generated by the FbDataAdapter specified in this property.
				if (dataAdapter != null)
				{
					dataAdapter.RowUpdating += new FbRowUpdatingEventHandler (RowUpdatingHandler);
				}
			}
		}
		
		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="P:QuotePrefix"]/*'/>
		public string QuotePrefix
		{
			get { return quotePrefix; }
			set
			{
				if (insertCommand != null || updateCommand != null || deleteCommand != null)
				{
					throw new InvalidOperationException("This property cannot be changed after an insert, update, or delete command has been generated.");
				}
				
				quotePrefix = value;
			}
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="P:QuoteSuffix"]/*'/>
		public string QuoteSuffix
		{
			get { return quoteSuffix; }
			set
			{
				if (insertCommand != null || updateCommand != null || deleteCommand != null)
				{
					throw new InvalidOperationException("This property cannot be changed after an insert, update, or delete command has been generated.");
				}
				
				quoteSuffix = value;
			}
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="P:SelectCommand"]/*'/>
		private FbCommand SelectCommand
		{
			get
			{
				if (dataAdapter.SelectCommand != null)
				{
					return dataAdapter.SelectCommand;
				}

				return null;
			}
		}


		#endregion

		#region CONSTRUCTORS

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:#ctor"]/*'/>
		public FbCommandBuilder()
		{			
			sqlInsert		= "INSERT INTO {0} ({1}) VALUES ({2})";
			sqlUpdate		= "UPDATE {0} SET {1} WHERE {2}";
			sqlDelete		= "DELETE FROM {0} WHERE {1}";
			whereClausule	= "{0} = ?";
			setClausule		= "{0} = ?";
			quotePrefix		= "\"";
			quoteSuffix		= "\"";
			disposed		= false;
		}
		
		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:#ctor(System.Data.IDbDataAdapter)"]/*'/>
		public FbCommandBuilder(FbDataAdapter adapter) : this()
		{
			this.DataAdapter    = adapter;
		}

		#endregion

		#region DESTRUCTORS

		/// <include file='xmldoc/fbcommand.xml' path='doc/member[@name="M:Finalize"]/*'/>
		~FbCommandBuilder() 
		{
			Dispose(false);
		}

		/// <include file='xmldoc/fbcommand.xml' path='doc/member[@name="M:Dispose(System.Boolean)"]/*'/>
		protected override void Dispose(bool disposing)
		{
			if (!disposed)
			{
				try
				{
					if (disposing)
					{
						if (insertCommand != null)
						{
							insertCommand.Dispose();
						}
						if (updateCommand != null)
						{
							updateCommand.Dispose();
						}
						if (deleteCommand != null)
						{
							deleteCommand.Dispose();
						}
						if (schemaTable != null)
						{
							schemaTable.Dispose();
						}
					}
					
					// release any unmanaged resources
					
					disposed = true;
				}
				finally 
				{
					base.Dispose(disposing);
				}
			}
		}

		#endregion

		#region METHODS
		
		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:DeriveParameters(System.Data.FbCommand)"]/*'/>
		public static void DeriveParameters(FbCommand command)
		{
			if (command.CommandType != CommandType.StoredProcedure)
			{
				throw new InvalidOperationException("The command text is not a valid stored procedure name.");
			}

			command.Parameters.Clear();
			command.Prepare();

			XSQLVAR[] sqlvar = command.Statement.Statement.InSqlda.sqlvar;

			for (int i = 0; i < sqlvar.Length; i++)
			{
				command.Parameters.Add("@ip" + i.ToString(),
					FbField.GetFbType(sqlvar[i].sqltype,sqlvar[i].sqlscale,sqlvar[i].sqlsubtype),
					sqlvar[i].sqlname);
			}

			sqlvar = null;
			sqlvar = command.Statement.Statement.OutSqlda.sqlvar;

			int index = command.Parameters.Count;

			for (int i = 0; i < sqlvar.Length; i++)
			{
				command.Parameters.Add("@op" + index.ToString(),
					FbField.GetFbType(sqlvar[i].sqltype,sqlvar[i].sqlscale,
					sqlvar[i].sqlsubtype),
					sqlvar[i].sqlname).Direction = ParameterDirection.Output;

				index++;
			}
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:GetInsertCommand"]/*'/>
		public FbCommand GetInsertCommand()
		{			
			if (insertCommand == null)
			{
				BuildSchemaTable();
				BuildInsertCommand(null, null);
			}
			
			return insertCommand;
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:GetUpdateCommand"]/*'/>
		public FbCommand GetUpdateCommand()
		{			
			if (updateCommand == null)
			{
				BuildSchemaTable();
				BuildUpdateCommand(null, null);
			}
			
			return updateCommand;
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:GetDeleteCommand"]/*'/>
		public FbCommand GetDeleteCommand()
		{			
			if (deleteCommand == null)
			{
				BuildSchemaTable();
				BuildDeleteCommand(null, null);
			}
			
			return deleteCommand;
		}
		
		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:RefreshSchema"]/*'/>
		public void RefreshSchema()
		{
			insertCommand = null;
			updateCommand = null;
			deleteCommand = null;			
			schemaTable   = null;
		}

		#endregion

		#region BUILD_COMMAND_METHODS

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:BuildInsertCommand(System.Data.DataRow,System.Data.Common.DataTableMapping)"]/*'/>
		private FbCommand BuildInsertCommand(DataRow row, DataTableMapping tableMapping)
		{						
			StringBuilder	sql		= new StringBuilder();
			StringBuilder	fields	= new StringBuilder();
			StringBuilder	values	= new StringBuilder();
			string			dsColumnName = String.Empty;

			insertCommand = new FbCommand(sql.ToString(), SelectCommand.Connection, SelectCommand.Transaction);

			int i = 0;
			foreach (DataRow schemaRow in schemaTable.Rows)
			{				
				if (IsUpdatable(schemaRow, row))
				{
					if (fields.Length > 0)
					{
						fields.Append(", ");
					}
					if (values.Length > 0)
					{
						values.Append(", ");
					}

					// Build Field name and append it to the string
					fields.Append(quotePrefix + schemaRow["BaseColumnName"] + quoteSuffix);
					
					// Build value name and append it to the string
					values.Append("?");
					
					FbParameter parameter = CreateParameter(schemaRow, i, false);

					if (tableMapping != null)
					{
						if (tableMapping.ColumnMappings.Count > 0)
						{
							dsColumnName = tableMapping.ColumnMappings[parameter.SourceColumn].DataSetColumn;
						}
						else
						{
							dsColumnName = parameter.SourceColumn;
						}
					}
					else
					{
						dsColumnName = parameter.SourceColumn;
					}

					if (row != null)
					{
						parameter.Value = row[dsColumnName];
					}

					i++;

					insertCommand.Parameters.Add(parameter);
				}
			}

			sql.AppendFormat(sqlInsert, tableName, fields.ToString(), values.ToString());

			insertCommand.CommandText = sql.ToString();

			// None is the Default value for automatically generated commands
			insertCommand.UpdatedRowSource = UpdateRowSource.None;

			return insertCommand;
		}
		
		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:BuildUpdateCommand(System.Data.DataRow,System.Data.Common.DataTableMapping)"]/*'/>
		public FbCommand BuildUpdateCommand(DataRow row, DataTableMapping tableMapping)
		{
			StringBuilder sql			= new StringBuilder();
			StringBuilder sets			= new StringBuilder();
			StringBuilder where			= new StringBuilder();
			string		  dsColumnName	= String.Empty;

			if (!hasPrimaryKey)
			{
				throw new InvalidOperationException ("Dynamic SQL generation for the UpdateCommand is not supported against a SelectCommand that does not return any key column information.");
			}

			updateCommand = new FbCommand(sql.ToString(), SelectCommand.Connection, SelectCommand.Transaction);

			int i = 0;
			foreach (DataRow schemaRow in schemaTable.Rows)
			{				
				if (IsUpdatable(schemaRow, row))
				{
					if (sets.Length > 0)
					{
						sets.Append(", ");
					}

					// Build Field name and append it to the string
					sets.AppendFormat(setClausule, quotePrefix + schemaRow["BaseColumnName"] + quoteSuffix);

					FbParameter parameter = CreateParameter(schemaRow, i, false);
										
					if (tableMapping != null)
					{
						if (tableMapping.ColumnMappings.Count > 0)
						{
							dsColumnName = tableMapping.ColumnMappings[parameter.SourceColumn].DataSetColumn;
						}
						else
						{
							dsColumnName = parameter.SourceColumn;
						}
					}
					else
					{
						dsColumnName = parameter.SourceColumn;
					}

					if (row != null)
					{
						parameter.Value = row[dsColumnName];
					}

					i++;

					updateCommand.Parameters.Add(parameter);
				}				
			}
			
			// Build where clausule
			foreach (DataRow schemaRow in schemaTable.Rows)
			{				
				if (IncludedInWhereClause (schemaRow)) 
				{
					if (where.Length > 0)
					{
						where.Append(" AND ");
					}
					
					where.AppendFormat(whereClausule, quotePrefix + schemaRow["BaseColumnName"] + quoteSuffix);

					FbParameter parameter = CreateParameter(schemaRow, i, true);

					if (tableMapping != null)
					{
						if (tableMapping.ColumnMappings.Count > 0)
						{
							dsColumnName = tableMapping.ColumnMappings[parameter.SourceColumn].DataSetColumn;
						}
						else
						{
							dsColumnName = parameter.SourceColumn;
						}
					}
					else
					{
						dsColumnName = parameter.SourceColumn;
					}

					if (row != null)
					{
						parameter.Value = row[dsColumnName, DataRowVersion.Original];
					}						

					i++;

					updateCommand.Parameters.Add(parameter);
				}				
			}

			sql.AppendFormat(sqlUpdate, tableName, sets.ToString(), where.ToString());
			
			updateCommand.CommandText = sql.ToString();

			// None is the Default value for automatically generated commands
			updateCommand.UpdatedRowSource = UpdateRowSource.None;

			return updateCommand;
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:BuildDeleteCommand(System.Data.DataRow,System.Data.Common.DataTableMapping)"]/*'/>
		public FbCommand BuildDeleteCommand(DataRow row, DataTableMapping tableMapping)
		{
			StringBuilder sql	= new StringBuilder();
			StringBuilder where = new StringBuilder();
			string		  dsColumnName = String.Empty;

			if (!hasPrimaryKey)
			{
				throw new InvalidOperationException ("Dynamic SQL generation for the DeleteCommand is not supported against a SelectCommand that does not return any key column information.");
			}

			deleteCommand = new FbCommand(sql.ToString(), SelectCommand.Connection, SelectCommand.Transaction);
		
			// Build where clausule
			int i = 0;
			foreach (DataRow schemaRow in schemaTable.Rows)
			{				
				if (IncludedInWhereClause (schemaRow)) 
				{
					if (where.Length > 0)
					{
						where.Append(" AND ");
					}
					
					where.AppendFormat(whereClausule, quotePrefix + schemaRow["BaseColumnName"] + quoteSuffix);
					
					FbParameter parameter = CreateParameter(schemaRow, i, true);

					if (tableMapping != null)
					{
						if (tableMapping.ColumnMappings.Count > 0)
						{
							dsColumnName = tableMapping.ColumnMappings[parameter.SourceColumn].DataSetColumn;
						}
						else
						{
							dsColumnName = parameter.SourceColumn;
						}
					}
					else
					{
						dsColumnName = parameter.SourceColumn;
					}
					
					if (row != null)
					{
						parameter.Value = row[dsColumnName, DataRowVersion.Original];
					}

					i++;

					deleteCommand.Parameters.Add(parameter);
				}
			}

			sql.AppendFormat(sqlDelete, tableName, where.ToString());
			
			deleteCommand.CommandText = sql.ToString();

			// None is the Default value for automatically generated commands
			deleteCommand.UpdatedRowSource = UpdateRowSource.None;

			return deleteCommand;
		}

		private FbParameter CreateParameter(DataRow schemaRow, int index, bool isWhereParameter)
		{
			FbParameter parameter = new FbParameter(String.Format("@p{0}", index), (FbType)schemaRow["ProviderType"]);

			parameter.Size			= Convert.ToInt32(schemaRow["ColumnSize"]);
			if (schemaRow["NumericPrecision"] != DBNull.Value)
			{
				parameter.Precision	= Convert.ToByte(schemaRow["NumericPrecision"]);
			}
			if (schemaRow["NumericScale"] != DBNull.Value)
			{
				int multiplier	= 1;
				int scale		= (int)schemaRow["NumericScale"];
				if (scale < 0)
				{
					multiplier = -1;
				}
				parameter.Scale		= Convert.ToByte(scale*multiplier);
			}
			parameter.SourceColumn	= Convert.ToString(schemaRow["BaseColumnName"]);

			if (isWhereParameter)
			{
				parameter.SourceVersion	= DataRowVersion.Original;
			}
			else
			{
				parameter.SourceVersion	= DataRowVersion.Current;
			}

			return parameter;
		}


		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:IsUpdatable(System.Data.DataRow, System.Data.DataRow)"]/*'/>
		private bool IsUpdatable(DataRow schemaRow, DataRow row)
		{
			if (row != null)
			{
				string		columnName	= (string) schemaRow["ColumnName"];
				DataColumn	column		= row.Table.Columns[columnName];

				if (column != null)
				{
					if (column.Expression != String.Empty)
					{
						return false;
					}
					if (column.AutoIncrement)
					{
						return false;
					}
					if (column.ReadOnly)
					{
						return false;
					}
				}
			}

			if ((bool) schemaRow["IsExpression"])
			{
				return false;
			}
			if ((bool) schemaRow["IsAutoIncrement"])
			{
				return false;
			}
			if ((bool) schemaRow["IsRowVersion"])
			{
				return false;
			}
			if ((bool) schemaRow["IsReadOnly"])
			{
				return false;
			}

			return true;
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:IncludedInWhereClause(System.Data.DataRow)"]/*'/>
		private bool IncludedInWhereClause(DataRow schemaRow)
		{
			if (!(bool)schemaRow["IsKey"])
			{
				return false;
			}

			if ((bool)schemaRow["IsLong"])
			{
				return false;
			}

			return true;
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:BuldSchemaTable"]/*'/>
		private void BuildSchemaTable()
		{
			if (SelectCommand == null)
			{
				throw new InvalidOperationException("The DataAdapter.SelectCommand property needs to be initialized.");
			}
			if (SelectCommand.Connection == null)
			{
				throw new InvalidOperationException ("The DataAdapter.SelectCommand.Connection property needs to be initialized.");
			}

			if (schemaTable == null)
			{				
				FbDataReader reader = SelectCommand.ExecuteReader(CommandBehavior.SchemaOnly);
				schemaTable = reader.GetSchemaTable();
				reader.Close();

				CheckSchemaTable();
			}			
		}

		/// <include file='xmldoc/fbcommandbuilder.xml' path='doc/member[@name="M:CheckSchemaTable"]/*'/>
		private void CheckSchemaTable()
		{
			tableName		= String.Empty;
			hasPrimaryKey	= false;

			foreach (DataRow schemaRow in schemaTable.Rows)
			{
				if (tableName == String.Empty)
				{
					tableName = (string)schemaRow["BaseTableName"];
				}
				if (tableName != (string)schemaRow["BaseTableName"] &&
					!(bool)schemaRow["IsExpression"])
				{
					throw new InvalidOperationException("Dynamic SQL generation is not supported against multiple base tables.");
				}
				if ((bool)schemaRow["IsKey"])
				{
					hasPrimaryKey = true;
				}
			}
		}

		#endregion

		#region EVENT_HANDLER

		private void RowUpdatingHandler (object sender, FbRowUpdatingEventArgs e)
		{
			if (e.Status != UpdateStatus.Continue)
			{
				return;
			}

			switch (e.StatementType) 
			{
				case StatementType.Insert:
					insertCommand = e.Command;
					break;

				case StatementType.Update:
					updateCommand = e.Command;
					break;

				case StatementType.Delete:
					deleteCommand = e.Command;
					break;

				default:
					return;
			}

			try 
			{
				BuildSchemaTable();

				switch (e.StatementType) 
				{
					case StatementType.Insert:
						e.Command = BuildInsertCommand(e.Row, e.TableMapping);
						e.Status  = UpdateStatus.Continue;
						break;

					case StatementType.Update:
						e.Command = BuildUpdateCommand(e.Row, e.TableMapping);
						e.Status  = UpdateStatus.Continue;
						break;
					
					case StatementType.Delete:
						e.Command = BuildDeleteCommand(e.Row, e.TableMapping);
						e.Status  = UpdateStatus.Continue;
						break;
				}

				if (e.Command != null && e.Row != null) 
				{					
					e.Row.AcceptChanges();
					e.Status = UpdateStatus.Continue;
				}
			}
			catch (Exception exception) 
			{
				e.Errors = exception;
				e.Status = UpdateStatus.ErrorsOccurred;
			}
		}

		#endregion
	}
}
