using Microsoft.VisualStudio.TestTools.UnitTesting;
using GroundTerminalApp;

namespace GroundTerminalApp.Tests
{
    [TestClass]
    internal class SearchingPageTests
    {
        private SearchingPageApp searchingPageApp;

        /*
         *  METHOD          : TestSetup
         *  CATEGORY        : STRUCTURAL / INITIALIZATION
         *  PARAMETERS      : None
         *  PURPOSE         : Initializes the SearchingPageApp instance before each test run.
         *                    Dashboard reference is set to null for logic-only testing.
         */
        [TestInitialize]
        public void TestSetup()
        {
            searchingPageApp = new SearchingPageApp(null);

      
        }

        /*
         *  METHOD          : ParseInt_ShouldReturnExpected
         *  CATEGORY        : UNIT / STRUCTURAL / FUNCTIONAL
         *  PARAMETERS      : None (uses inline test cases)
         *  PURPOSE         : Validates that the ParseInt method correctly handles valid and invalid string inputs.
         *                    Ensures safe parsing behavior and fallback to zero for malformed or empty values.
         */
        [TestMethod]
        public void ParseInt_ShouldReturnExpected()
        {
         
            // Valid inputs
            Assert.AreEqual(123, searchingPageApp.ParseInt("123"));          // Standard numeric string
            Assert.AreEqual(45, searchingPageApp.ParseInt("  45 "));         // Input with leading/trailing spaces

            // Invalid inputs
            Assert.AreEqual(0, searchingPageApp.ParseInt("-"));              // Dash-only string
            Assert.AreEqual(0, searchingPageApp.ParseInt(""));               // Empty string
            Assert.AreEqual(0, searchingPageApp.ParseInt(null));             // Null input
            Assert.AreEqual(0, searchingPageApp.ParseInt("abc"));            // Non-numeric string
        }

    }
}
