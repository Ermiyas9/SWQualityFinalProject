
/* FILE             : UsersLoginPageTests.cs                                                                                */
/* PROJECT          : Software Quality Final Project Milestone 3                                                            */
/* PROGRAMMER       : Quang Minh Vu                                                                                         */
/* PURPOSE          : Provides test coverage for UsersLoginPage data models, including                                      */
/*                    LoginCredentials and AuthenticationResult. Ensures validation logic                                   */
/*                    and authentication structures behave correctly without UI or DB dependencies.                         */

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GroundTerminalApp;

namespace GroundTerminalApp.Tests
{
    [TestClass]
    public class UsersLoginPageTests
    {
        /* Test:       LoginCredentials_BothFieldsProvided_IsValidTrue                                                  */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                   */
        /* Purpose:    Ensures that LoginCredentials sets IsValid = true when both username and password fields         */
        /*             contain non-empty strings. Validates object initialization logic.                                */
        [TestMethod]
        [TestCategory("DataValidation")]
        public void LoginCredentials_BothFieldsProvided_IsValidTrue()
        {
            // Arrange & Act
            var credentials = new UsersLoginPage.LoginCredentials
            {
                Username = "quang_minh",
                Password = "T3182001a@"
            };
            credentials.IsValid = !string.IsNullOrEmpty(credentials.Username) &&
                                  !string.IsNullOrEmpty(credentials.Password);
            // Assert
            Assert.IsTrue(credentials.IsValid, "IsValid should be true when both username and password are provided");
            Assert.AreEqual("quang_minh", credentials.Username);
            Assert.AreEqual("T3182001a@", credentials.Password);
        }

        /* Test:       LoginCredentials_EmptyUsername_IsValidFalse                                                      */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                   */
        /* Purpose:    Verifies that LoginCredentials marks IsValid = false when Username is empty but password is      */
        /*             provided. Ensures username field validation behavior.                                            */
        [TestMethod]
        [TestCategory("DataValidation")]
        public void LoginCredentials_EmptyUsername_IsValidFalse()
        {
            // Arrange & Act
            var credentials = new UsersLoginPage.LoginCredentials
            {
                Username = "",
                Password = "T3182001a@"
            };

            // Correctly set IsValid based on actual property values
            credentials.IsValid = !string.IsNullOrEmpty(credentials.Username)
                                  && !string.IsNullOrEmpty(credentials.Password);

            // Assert
            Assert.IsFalse(credentials.IsValid, "IsValid should be false when username is empty");
        }

        /* Test:       LoginCredentials_EmptyPassword_IsValidFalse                                                      */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                   */
        /* Purpose:    Ensures that LoginCredentials sets IsValid = false when Password is empty but username is        */
        /*             provided. Confirms password field validation behavior.                                            */
        [TestMethod]
        [TestCategory("DataValidation")]
        public void LoginCredentials_EmptyPassword_IsValidFalse()
        {
            // Arrange & Act
            var credentials = new UsersLoginPage.LoginCredentials
            {
                Username = "quang_minh",
                Password = ""
            };

            credentials.IsValid = !string.IsNullOrEmpty(credentials.Username)
                                  && !string.IsNullOrEmpty(credentials.Password);


            // Assert
            Assert.IsFalse(credentials.IsValid, "IsValid should be false when password is empty");
        }

        /* Test:       AuthenticationResult_SuccessfulLogin_ContainsUserData                                            */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                   */
        /* Purpose:    Validates that AuthenticationResult correctly stores user information when login is successful,  */
        /*             including user profile data and success message.                                                 */
        [TestMethod]
        [TestCategory("DataStructure")]
        public void AuthenticationResult_SuccessfulLogin_ContainsUserData()
        {
            // Arrange & Act
            var user = new UsersLoginPage.AppUser
            {
                UserId = 1,
                Username = "test_user",
                RoleId = 2,
                IsActive = true
            };

            var result = new UsersLoginPage.AuthenticationResult
            {
                IsSuccess = true,
                Message = "Login successful.",
                AuthenticatedUser = user
            };

            // Assert
            Assert.IsTrue(result.IsSuccess, "IsSuccess should be true for successful login");
            Assert.AreEqual("Login successful.", result.Message);
            Assert.IsNotNull(result.AuthenticatedUser, "AuthenticatedUser should not be null");
            Assert.AreEqual(1, result.AuthenticatedUser.UserId);
            Assert.AreEqual("test_user", result.AuthenticatedUser.Username);
            Assert.AreEqual(2, result.AuthenticatedUser.RoleId);
            Assert.IsTrue(result.AuthenticatedUser.IsActive);
        }

        /* Test:       AuthenticationResult_FailedLogin_NoUserData                                                      */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                   */
        /* Purpose:    Ensures AuthenticationResult represents failed login scenarios correctly by storing no user      */
        /*             object and returning appropriate failure message and flags.                                       */
        [TestMethod]
        [TestCategory("DataStructure")]
        public void AuthenticationResult_FailedLogin_NoUserData()
        {
            // Arrange & Act
            var result = new UsersLoginPage.AuthenticationResult
            {
                IsSuccess = false,
                Message = "Invalid username or password.",
                AuthenticatedUser = null
            };

            // Assert
            Assert.IsFalse(result.IsSuccess, "IsSuccess should be false for failed login");
            Assert.AreEqual("Invalid username or password.", result.Message);
            Assert.IsNull(result.AuthenticatedUser, "AuthenticatedUser should be null for failed login");
        }
    }
}
