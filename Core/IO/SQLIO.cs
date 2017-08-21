﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace GraphicsTestFramework.SQL
{	
	public class SQLIO : MonoBehaviour {

		public static SQLIO _Instance = null;//Instance

		public static SQLIO Instance {
			get {
				if (_Instance == null)
					_Instance = (SQLIO)FindObjectOfType (typeof(SQLIO));
				return _Instance;
			}
		}

		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
		//CONNECTION VARIABLES
		private string _conString = @"user id=UTF_admin;" +
		                            @"password=chicken22;data source=10.44.41.115;" +
		                            @"database=UTF_testbed;";
		private SqlConnection _connection = null;

		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
		//LOCAL VARIABLES
		public connectionStatus liveConnection;
		private NetworkReachability netStat = NetworkReachability.NotReachable;
		private List<string> SQLQueryBackup = new List<string>();

		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
		//INFORMATION
		private SystemData sysData;//local version of systemData

		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
		//Base Methods
		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

		void Start(){
			InvokeRepeating ("CheckConnection", 0f, 5f); //Invoke network check
		}

		//Init gets called from ResultsIO
		public void Init (SystemData systemData)
		{
			sysData = systemData;
			_connection = new SqlConnection (_conString);//Create a new connnection with the _consString
		}

		void OnEnable(){
			//OpenConnection (_connection); //Try open a conneciton to DB
		}

		void OnDisable(){
			CloseConnection (_connection);//Close SQL conneciton on disable
		}

		//Opens a connection to the SQL DB
		void OpenConnection(SqlConnection connection){
			try
			{
				connection.Open();//open the connection
			}
			catch(Exception e)
			{
				Console.Instance.Write(DebugLevel.Critical, MessageLevel.LogWarning, e.ToString());
			}
			Console.Instance.Write (DebugLevel.File, MessageLevel.Log, "SQL connection is:" + connection.State.ToString ());//write the connection state to log
		}

		//Closes the connection to the SQL DB
		void CloseConnection(SqlConnection connection){
			try
			{
				connection.Close ();//close the connection
			}
			catch(Exception e)
			{
				Console.Instance.Write(DebugLevel.Critical, MessageLevel.LogWarning, e.ToString());
			}
			Console.Instance.Write (DebugLevel.File, MessageLevel.Log, "SQL connection is:" + connection.State.ToString ());//write the connection state to log
		}

		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
		// Query methods - TODO wip
		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

		//simple test query, no return
		public string SQLQuery ( string _query ) {
			Console.Instance.Write (DebugLevel.File, MessageLevel.Log, "SQL Query Out:" + _query);
			try {
				SqlCommand cmd = new SqlCommand(_query, _connection);
				string _returnQuery = (string) cmd.ExecuteScalar ();
				return _returnQuery;
				cmd.Dispose ();
			}
			catch (SqlException _exception) {
				Debug.LogWarning(_exception.ToString());
				return null;
			}
		}

		public int SQLNonQuery(string _input){
			Console.Instance.Write (DebugLevel.File, MessageLevel.Log, "SQL Non Query Out:" + _input);
			try {
				SqlCommand cmd = new SqlCommand(_input, _connection);
				cmd.CommandTimeout = 300;
				int _rowsChanged = 
					cmd.ExecuteNonQuery ();
				return _rowsChanged;
				cmd.Dispose ();
			}
			catch (SqlException _exception) {
				Debug.LogWarning(_exception.ToString());
				return -1;
			}
		}

		public SqlDataReader SQLRequest(string _query){
			Console.Instance.Write (DebugLevel.File, MessageLevel.Log, "SQL Request Query Out:" + _query);
			try
			{
				SqlDataReader myReader = null;
				SqlCommand    myCommand = new SqlCommand(_query, _connection);
				myReader = myCommand.ExecuteReader();
				return myReader;
			}
			catch (Exception e)
			{
				Debug.Log(e.ToString());
				return null;
			}
		}

		//Testing method TODO - delete me
		[ContextMenu("DoThing")]
		public void DoThing(){
			//CreateTable ("Example", new string[]{"Hello", "Name", "Age"});
			GetbaselineTimestamp ("Debug");
		}

		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
		// Query data - TODO wip
		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

		//Check to see if table exists(not ready) TODO
		public bool TableExists(string tableName){
			string s = SQLQuery ("IF object_id('dbo." + tableName + "') is not null PRINT 'HERE'");
			Debug.Log (s);
			return true;
		}

		//Gets the timestamp in DateTime format from the server
		public DateTime GetbaselineTimestamp(string suiteName){
			DateTime timestamp = DateTime.MinValue;//make date time min, we check this on the other end since it is not nullable
			SqlDataReader reader = SQLRequest ("SELECT * FROM SuiteBaselineTimestamps WHERE api='" + sysData.API + "' AND suiteName='" + suiteName + "' AND platform='" + sysData.Platform + "';");
			while(reader.Read ()){
				timestamp = reader.GetDateTime (3);
			}
			reader.Close ();//close the reader after getting hte information
			return timestamp;
		}

		//fetch the server side baselines
		public ResultsIOData[] FetchBaselines(string[] suiteNames, string platform, string api){
			List<ResultsIOData> data = new List<ResultsIOData> ();
			List<string> tables = new List<string>();
			//Get the table names to pull baselines from
			foreach(string suite in suiteNames){
				SqlDataReader reader = SQLRequest (String.Format ("SELECT name FROM sys.tables WHERE name LIKE '{0}%Baseline'", suite));//select any tables with the suite name in it
				while(reader.Read ()){
					tables.Add (reader.GetString (0));//add the table name to the list to pull from
				}reader.Close ();
			}
			//string lastSuite = "";
			//string lastTestType = "";
			int n = 0;
			foreach(string table in tables){
				string suite = table.Substring (0, table.IndexOf ("_"));
				string testType = table.Substring (table.IndexOf ("_") + 1, table.LastIndexOf ("_") - (suite.Length + 1));
				data.Add (new ResultsIOData());
				data [n].suite = suite;
				data [n].testType = testType;
				SqlDataReader reader = SQLRequest (String.Format ("SELECT * FROM {0} WHERE platform='{1}' AND api='{2}'", table, platform, api));
				//ResultsDataCommon dataCommon = new ResultsDataCommon ();
				while(reader.Read ()){
					ResultsIORow row = new ResultsIORow();
					for(int i = 0; i < reader.FieldCount; i++){
						if(data[n].fieldNames.Count != reader.FieldCount)
							data[n].fieldNames.Add (reader.GetName (i));
						row.resultsColumn.Add (reader.GetValue (i).ToString ());//add the table name to the list to pull from
					}
					data[n].resultsRow.Add (row);
				}reader.Close ();
				n++;
			}
			return data.ToArray ();
		}

		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
		// Sending data - TODO wip
		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

		//Creates a new table named 'tableName' and with the columns in 'columns'
		public void CreateTable(string tableName, string[] columns){
			string _columns = CreateColumns (columns);//gets a string formatted with data types
			string _stringRequest = SQLQuery("CREATE TABLE " + tableName + " (" + _columns + ");");
		}

		//Set the suite baseline timestamp
		public void SetSuiteTimestamp(SuiteBaselineData SBD){
			StringBuilder outputString = new StringBuilder();
			string tableName = "SuiteBaselineTimestamps";
			List<string> values = new List<string> (){ SBD.suiteName, SBD.platform, SBD.api, SBD.suiteTimestamp};
			string[] fields = new string[]{ "suiteName", "platform", "api", "suiteTimestamp"};
			//condition string
			string comparisonString = "platform='" + SBD.platform +
				"' AND api='" + SBD.api + 
				"' AND suiteName='" + SBD.suiteName +
				"'";//the condition to match

			outputString.Append ("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE BEGIN TRANSACTION ");//using transaction and isolation to avoid double write issues
			outputString.AppendFormat ("IF EXISTS (select 1 from {0} WHERE {2}) BEGIN UPDATE {0} SET {3} WHERE {2} END ELSE INSERT INTO {0} VALUES ({1});", new object[]{tableName, ConvertToValues (values), comparisonString, ConvertToValues (values, fields)});
			outputString.Append (" COMMIT TRANSACTION");
			SQLQuery (outputString.ToString ());//send the query
		}

		//Creates an entry of either result or baseline(replaces UploadData from old system)
		public IEnumerator AddEntry(ResultsIOData inputData, string tableName, int baseline, bool batch){
			Console.Instance.Write (DebugLevel.File, MessageLevel.Log, "Starting SQL query creation"); // Write to console
			StringBuilder outputString = new StringBuilder();
			CreateTable (tableName, inputData.fieldNames.ToArray ());
			int rowNum = 0;

			if(baseline == 1){//baseline sorting
				//condition string
				if(batch)
					outputString.Append ("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE BEGIN TRANSACTION ");//using transaction and isolation to avoid double write issues

				foreach (ResultsIORow row in inputData.resultsRow) {
					rowNum++;
					//condition string
					if(!batch)
						outputString.Append ("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE BEGIN TRANSACTION ");//using transaction and isolation to avoid double write issues

					string comparisonString = "Platform='" + row.resultsColumn [5] +
					                          "' AND API='" + row.resultsColumn [6] +
					                          "' AND RenderPipe='" + row.resultsColumn [7] +
					                          "' AND GroupName='" + row.resultsColumn [8] +
					                          "' AND TestName='" + row.resultsColumn [9] +
					                          "'";//the condition to match
				
					outputString.AppendFormat ("IF EXISTS (select 1 from {0} WHERE {2}) BEGIN UPDATE {0} SET {3} WHERE {2} END ELSE INSERT INTO {0} VALUES ({1});", new object[] {
						tableName,
						ConvertToValues (row.resultsColumn),
						comparisonString,
						ConvertToValues (row.resultsColumn, inputData.fieldNames.ToArray ())
					});

					if (!batch) {
						outputString.Append (" COMMIT TRANSACTION");
						if (liveConnection == connectionStatus.Server)
							SQLQuery (outputString.ToString ());//send the query
						else
							SQLQueryBackup.Insert (0, outputString.ToString ());//backup the query to send when connection is resumed
					}

				}

				if(batch)
					outputString.Append (" COMMIT TRANSACTION");
			}else{//result sorting
				foreach (ResultsIORow row in inputData.resultsRow) {
					rowNum++;
					if(batch)
						outputString.AppendFormat ("INSERT INTO {0} VALUES ({1});", tableName, ConvertToValues (row.resultsColumn));//using the insert function
					else{
						Debug.Log ("Sending row " + rowNum);
						int num;
						if (liveConnection == connectionStatus.Server) {
							Debug.LogWarning(row.resultsColumn [row.resultsColumn.Count - 1].Length);// TODO - temp cell count
							num = SQLNonQuery (string.Format ("INSERT INTO {0} VALUES ({1});", tableName, ConvertToValues (row.resultsColumn)));//send the query
							if (num == -1) {
								Debug.LogError ("Failed to upload 1st time");
								num = SQLNonQuery (string.Format ("INSERT INTO {0} VALUES ({1});", tableName, ConvertToValues (row.resultsColumn)));//send the query
								if(num == -1)
									Debug.LogError ("Failed to upload 2nd time");
							}
						}
						else
							SQLQueryBackup.Insert (0, string.Format ("INSERT INTO {0} VALUES ({1});", tableName, ConvertToValues (row.resultsColumn)));//backup the query to send when connection is resumed
					}
					yield return null;
				}
			}

			if (batch) {
				if (liveConnection == connectionStatus.Server)
					SQLQuery (outputString.ToString ());//send the query
			else
					SQLQueryBackup.Insert (0, outputString.ToString ());//backup the query to send when connection is resumed

				yield return new WaitForEndOfFrame ();
			}
		}

		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
		// Utilities - TODO wip
		// ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

		//Method to check for valid connection, Invoked from start
		void CheckConnection(){
			if(_connection == null)
				_connection = new SqlConnection (_conString);
			
			if(netStat != Application.internetReachability) {
				netStat = Application.internetReachability; //Get network state

				switch (netStat) {
				case NetworkReachability.NotReachable:
					Console.Instance.Write (DebugLevel.Key, MessageLevel.LogError, "Internet Connection Lost");
					liveConnection = connectionStatus.None; // connection is not available
					break;
				case NetworkReachability.ReachableViaCarrierDataNetwork:
					Console.Instance.Write (DebugLevel.Key, MessageLevel.LogError, "Internet Connection Not Reliable, Please connect to Wi-fi");
					liveConnection = connectionStatus.Mobile; // connection is not available
					break;
				case NetworkReachability.ReachableViaLocalAreaNetwork:
					Console.Instance.Write (DebugLevel.Key, MessageLevel.Log, "Internet Connection Live");
					liveConnection = connectionStatus.Internet; // connection is not available
					OpenConnection (_connection);//try open a connection to the server
					break;
				}
			}

			if (liveConnection == connectionStatus.Internet && _connection.State == ConnectionState.Open)
				liveConnection = connectionStatus.Server;
			else if (liveConnection == connectionStatus.Internet && _connection.State == ConnectionState.Closed)
				liveConnection = connectionStatus.Internet;
			
		}

		//create column list for table creation, inclued data type
		string CreateColumns(string[] columns){
			StringBuilder sb = new StringBuilder ();
			for(int i = 0; i < columns.Length; i++){
				string dataType = "varchar(255)";
				if(i < 12)
					dataType = dataTypes[i];
				else
					dataType = "nvarchar(MAX)";
				
				sb.Append (columns[i] + " " + dataType);
				if (i != columns.Length - 1)
					sb.Append (',');
			}
			return sb.ToString ();
		}

		//create column list for un-named values
		string ConvertToValues(List<string> values){
			StringBuilder sb = new StringBuilder ();
			for (int i = 0; i < values.Count; i++) {
				sb.Append ("'" + values[i] + "'");
				if (i != values.Count - 1)
					sb.Append (',');
			}
			return sb.ToString ();
		}

		//create column list for named values
		string ConvertToValues(List<string> values, string[] fields){
			StringBuilder sb = new StringBuilder ();
			for (int i = 0; i < values.Count; i++) {
				sb.Append (fields[i] + "='" + values[i] + "' ");
				if (i != values.Count - 1)
					sb.Append (',');
			}
			return sb.ToString ();
		}

		//SQL data types for common
		private string[] dataTypes = new string[]{"DATETIME",//datetime
												"varchar(255)",//UnityVersion
												"varchar(10)",//AppVersion
												"varchar(255)",//OS
												"varchar(255)",//Device
												"varchar(255)",//Platform
												"varchar(50)",//API
												"varchar(128)",//RenderPipe
												"varchar(128)",//GroupName
												"varchar(128)",//TestName
												"varchar(16)",//PassFail
												"varchar(MAX)",//Custom
		};
	}
}
