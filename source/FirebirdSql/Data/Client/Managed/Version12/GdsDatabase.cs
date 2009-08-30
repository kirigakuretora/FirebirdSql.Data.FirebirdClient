/*
 *	Firebird ADO.NET Data provider for .NET and Mono 
 * 
 *	   The contents of this file are subject to the Initial 
 *	   Developer's Public License Version 1.0 (the "License"); 
 *	   you may not use this file except in compliance with the 
 *	   License. You may obtain a copy of the License at 
 *	   http://www.firebirdsql.org/index.php?op=doc&id=idpl
 *
 *	   Software distributed under the License is distributed on 
 *	   an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either 
 *	   express or implied. See the License for the specific 
 *	   language governing rights and limitations under the License.
 * 
 *	Copyright (c) 2009 Jiri Cincura (jiri@cincura.net) 
 *      
 *	All Rights Reserved.
 */

using System;
using System.Collections;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Net;
using System.Collections.Generic;

using FirebirdSql.Data.Common;

namespace FirebirdSql.Data.Client.Managed.Version12
{
	internal class GdsDatabase : Version11.GdsDatabase
	{
		public GdsDatabase(Version10.GdsConnection connection)
			: base(connection)
		{ }

		protected override void SendAttachToBuffer(DatabaseParameterBuffer dpb, string database)
		{
			// Attach to the database
			this.Write(IscCodes.op_attach);
			this.Write((int)0);				    // Database	object ID
			dpb.Append(IscCodes.isc_dpb_utf8_filename, 0);
			this.WriteBuffer(Encoding.UTF8.GetBytes(database));				// Database	PATH
			this.WriteBuffer(dpb.ToArray());	// DPB Parameter buffer
		}

		protected override void SendCreateToBuffer(DatabaseParameterBuffer dpb, string database)
		{
			this.Write(IscCodes.op_create);
			this.Write((int)0);
			dpb.Append(IscCodes.isc_dpb_utf8_filename, 0);
			this.WriteBuffer(Encoding.UTF8.GetBytes(database));
			this.WriteBuffer(dpb.ToArray());
		}
	}
}