/* ======================================================================================================================= */
/* FILE             : SearchingPageAppTests.cs                                                                             */
/* PROJECT          : GroundTerminalApp.Tests                                                                              */
/* NAMESPACE        : GroundTerminalApp.Tests                                                                              */
/* PROGRAMMER       : Ermiyas (Endalkachew) Gulti                                                                          */
/* FIRST VERSION    : 2025-11-22                                                                                           */
/* DESCRIPTION      : Unit/structural tests for SearchingPageApp parsing helpers.                                          */
/*                  : Tests cover GetTargetTableFromInput (private) and ParseInt (public).                                 */
/*                  : Tests instantiate SearchingPageApp without running its WPF constructor to avoid UI dependencies.     */
/* ======================================================================================================================= */

using System;
using System.Reflection;
using System.Runtime.Serialization; 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GroundTerminalApp;

namespace GroundTerminalApp.Tests
{
    [TestClass]
    public class SearchingPageAppTests
    {
        /*  
         *  METHOD          : CreateSearchPageInstance
         *  RETURN TYPE     : SearchingPageApp
         *  PARAMETERS      : none
         *  DESCRIPTION     : Creates an instance of SearchingPageApp without invoking its constructor.
         *                    This avoids WPF InitializeComponent and other UI dependencies so parsing logic
         *                    can be tested in isolation.
         */
        private static SearchingPageApp CreateSearchPageInstance()
        {
            return (SearchingPageApp)FormatterServices.GetUninitializedObject(typeof(SearchingPageApp));
        }

        /*  
         *  METHOD          : InvokePrivate
         *  RETURN TYPE     : object
         *  PARAMETERS      : object instance -> instance containing the private method
         *                  : string methodName -> name of the private method to invoke
         *                  : params object[] args -> arguments to pass to the method
         *  DESCRIPTION     : Uses reflection to locate and invoke a non-public instance method.
         *                    Asserts the method exists and returns the invoked result.
         */
        private static object InvokePrivate(object instance, string methodName, params object[] args)
        {
            var mi = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(mi, $"Method '{methodName}' not found.");
            return mi.Invoke(instance, args);
        }

        /*  
         *  METHOD          : GetTargetTableFromInput_ValidPrefixProvided_ReturnsCorrectTableIndex_Flight
         *  RETURN TYPE     : void
         *  PARAMETERS      : none
         *  DESCRIPTION     : Verifies that input starting with 'F' maps to table index 1 (Flight).
         */
        [TestMethod]
        public void GetTargetTableFromInput_ValidPrefixProvided_ReturnsCorrectTableIndex_Flight()
        {
            // creating the instance of the search page with out its character 
            var searchingPageApp = CreateSearchPageInstance();
            var result = (int)InvokePrivate(searchingPageApp, "GetTargetTableFromInput", "F123");
            Assert.AreEqual(1, result);
        }


        /*  
         *  METHOD          : GetTargetTableFromInput_ValidPrefixProvided_ReturnsCorrectTableIndex_Channel
         *  RETURN TYPE     : void
         *  PARAMETERS      : none
         *  DESCRIPTION     : Verifies that input starting with 'C' maps to table index 2 (Channel).
         */
        [TestMethod]
        public void GetTargetTableFromInput_ValidPrefixProvided_ReturnsCorrectTableIndex_Channel()
        {
            var searchingPageApp = CreateSearchPageInstance();
            var result = (int)InvokePrivate(searchingPageApp, "GetTargetTableFromInput", "C7");
            Assert.AreEqual(2, result);
        }


        /*  
         *  METHOD          : GetTargetTableFromInput_ValidPrefixProvided_ReturnsCorrectTableIndex_Telemetry
         *  RETURN TYPE     : void
         *  PARAMETERS      : none
         *  DESCRIPTION     : Verifies that input starting with 'T' maps to table index 3 (Telemetry).
         */
        [TestMethod]
        public void GetTargetTableFromInput_ValidPrefixProvided_ReturnsCorrectTableIndex_Telemetry()
        {
            var searchingPageApp = CreateSearchPageInstance();
            var result = (int)InvokePrivate(searchingPageApp, "GetTargetTableFromInput", "T25");
            Assert.AreEqual(3, result);
        }


        /*  
         *  METHOD          : GetTargetTableFromInput_InvalidPrefix_ReturnsZero
         *  RETURN TYPE     : void
         *  PARAMETERS      : none
         *  DESCRIPTION     : Sanity check that an invalid prefix returns 0.
         */
        [TestMethod]
        public void GetTargetTableFromInput_InvalidPrefix_ReturnsZero()
        {
            var searchingPageApp = CreateSearchPageInstance();
            var result = (int)InvokePrivate(searchingPageApp, "GetTargetTableFromInput", "X5");
            Assert.AreEqual(0, result);
        }

        /*  
         *  METHOD          : ParseInt_InputValueContainsInvalidCharacters_ReturnsZeroSafely
         *  RETURN TYPE     : void
         *  PARAMETERS      : string input -> test input containing invalid characters
         *  DESCRIPTION     : Data-driven test ensuring ParseInt returns 0 for malformed inputs.
         */
        [DataTestMethod]
        [DataRow("12a3")]     // char with int mix 
        [DataRow("abc")]      // string only 
        [DataRow(" ")]        // whitespace
        [DataRow("-")]        // dash only
        [DataRow("1-2")]      // number with dash
        [DataRow("!23")]      // exceptional characters 
        [DataRow("0x10")]     // hex-like string
        public void ParseInt_InputValueContainsInvalidCharacters_ReturnsZeroSafely(string input)
        {
            var searchingPageApp = CreateSearchPageInstance();
            var result = searchingPageApp.ParseInt(input);
            Assert.AreEqual(0, result);
        }


        /*  
         *  METHOD          : ParseInt_ValidIntegerStrings_ReturnsParsedValue
         *  RETURN TYPE     : void
         *  PARAMETERS      : string input -> valid integer string
         *                  : int expected -> expected parsed integer
         *  DESCRIPTION     : Positive control verifying ParseInt parses valid integer strings correctly.
         */
        [DataTestMethod]
        [DataRow("0", 0)]
        [DataRow("123", 123)]
        [DataRow(" 42 ", 42)]
        public void ParseInt_ValidIntegerStrings_ReturnsParsedValue(string input, int expected)
        {
            var searchingPageApp = CreateSearchPageInstance();
            var result = searchingPageApp.ParseInt(input);
            Assert.AreEqual(expected, result);
        }
    }
}
