/*
 * Filename:    LogEntryTests.cs
 * By:          Mher Keshishian
 * Date:        December 5, 2025
 * Description: Unit tests for the LogEntry model used by the logsPage window.
 *              These tests focus on timestamp formatting and property change notifications.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using GroundTerminalApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GroundTerminalApp.Tests
{
	[TestClass]
	public class LogEntryTests
	{
		/*
         * Test:       LogEntry_TimestampSet_UpdatesFormattedTimestamp
         * Category:   UNIT / STRUCTURAL / FUNCTIONAL
         * Purpose:    Verifies that when Timestamp is set, the FormattedTimestamp property
         *             returns the expected formatted string used by the UI.
         */
		[TestMethod]
		public void LogEntry_TimestampSet_UpdatesFormattedTimestamp()
		{
			// Arrange
			var entry = new LogEntry();
			var testTimestamp = new DateTime(2025, 12, 4, 14, 30, 0);

			// Act
			entry.Timestamp = testTimestamp;
			var formatted = entry.FormattedTimestamp;

			// Assert
			Assert.AreEqual("2025-12-04 14:30:00", formatted, "FormattedTimestamp should match the expected yyyy-MM-dd HH:mm:ss format.");
		}

		/*
         * Test:       LogEntry_SetMessage_RaisesPropertyChangedForMessage
         * Category:   UNIT / STRUCTURAL / FUNCTIONAL
         * Purpose:    Verifies that setting the Message property raises the PropertyChanged
         *             event with the correct property name for WPF data binding.
         */
		[TestMethod]
		public void LogEntry_SetMessage_RaisesPropertyChangedForMessage()
		{
			// Arrange
			var entry = new LogEntry();
			bool eventRaised = false;
			string raisedPropertyName = null;

			entry.PropertyChanged += (object sender, PropertyChangedEventArgs e) =>
			{
				eventRaised = true;
				raisedPropertyName = e.PropertyName;
			};

			// Act
			entry.Message = "Test log message";

			// Assert
			Assert.IsTrue(eventRaised, "Setting Message should raise PropertyChanged.");
			Assert.AreEqual("Message", raisedPropertyName, "PropertyChanged should be raised for the 'Message' property.");
		}
	}
}
