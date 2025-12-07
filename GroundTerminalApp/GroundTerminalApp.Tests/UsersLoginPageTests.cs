/*
 * Filename:    UsersLoginPageTests.cs
 * By:          Quang Minh Vu
 * Date:        December 6, 2025
 * Description: Unit tests for the UsersLoginPage model used by the usersPage window.
 *              These tests focus on the logic of the user login page, such as password validation and user creation.
 */

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GroundTerminalApp;

namespace GroundTerminalApp.Tests
{
    [TestClass]
    public class UsersLoginPageTests
    {
        /* Test:       ValidateUsername_EmptyString_ReturnsFalse                                                                    
         Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                              
         Purpose:    Ensures that empty or whitespace-only usernames are rejected by IsValidUsername().                           */
        [TestMethod]
        [TestCategory("InputValidation")]
        public void ValidateUsername_EmptyString_ReturnsFalse()
        {
            // Arrange
            string username = "";

            // Act
            bool result = ValidationHelper.IsValidUsername(username);

            // Assert
            Assert.IsFalse(result, "Empty username should be invalid");
        }

        /* Test:       ValidateUsername_ValidString_ReturnsTrue                                                                     */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                              */
        /* Purpose:    Verifies that a properly formatted username passes validation.                                               */
        [TestMethod]
        [TestCategory("InputValidation")]
        public void ValidateUsername_ValidString_ReturnsTrue()
        {
            // Arrange
            string username = "john_doe";

            // Act
            bool result = ValidationHelper.IsValidUsername(username);

            // Assert
            Assert.IsTrue(result, "Valid username should pass validation");
        }

        /* Test:       ValidatePassword_EmptyString_ReturnsFalse                                                                    */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                              */
        /* Purpose:    Ensures that empty or whitespace-only passwords are rejected by IsValidPassword().                          */
        [TestMethod]
        [TestCategory("InputValidation")]
        public void ValidatePassword_EmptyString_ReturnsFalse()
        {
            // Arrange
            string password = "";

            // Act
            bool result = ValidationHelper.IsValidPassword(password);

            // Assert
            Assert.IsFalse(result, "Empty password should be invalid");
        }

        /* Test:       SanitizeUsername_LeadingTrailingSpacesAndMixedCase_ReturnsTrimmedLowercase                                   */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                              */
        /* Purpose:    Validates that user credentials are properly sanitized: trimmed and lowercased.                             */
        [TestMethod]
        [TestCategory("DataProcessing")]
        public void SanitizeUsername_LeadingTrailingSpacesAndMixedCase_ReturnsTrimmedLowercase()
        {
            // Arrange
            string username = "  JoHn_DoE  ";

            // Act
            string result = ValidationHelper.SanitizeUsername(username);

            // Assert
            Assert.AreEqual("john_doe", result, "Username should be trimmed and converted to lowercase");
        }

        /* Test:       CheckPasswordStrength_StrongPassword_ReturnsStrong                                                           */
        /* Category:   UNIT / STRUCTURAL / FUNCTIONAL                                                                              */
        /* Purpose:    Ensures strong passwords (uppercase, lowercase, digits, special chars) are correctly classified as Strong.   */
        [TestMethod]
        [TestCategory("BusinessLogic")]
        public void CheckPasswordStrength_StrongPassword_ReturnsStrong()
        {
            // Arrange
            string password = "P@ssw0rd123!";

            // Act
            string result = PasswordHelper.CheckPasswordStrength(password);

            // Assert
            Assert.AreEqual("Strong", result, "Password with uppercase, lowercase, numbers, and special chars should be strong");
        }
    }

    /* HELPER CLASSES                                                                                                           */
    /* These simulate the validation + password logic that would normally exist in your application code.                        */
    /* Make sure to place real implementations inside actual app classes (UsersLoginPage or utilities).                          */

    public static class ValidationHelper
    {
        /* Function:    IsValidUsername                                                                                  */
        /* Purpose:     Ensures username is non-empty and within allowed character length.                                */
        /* Returns:     true if valid, otherwise false.                                                                    */
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            if (username.Length < 3 || username.Length > 50)
                return false;

            return true;
        }

        /* Function:    IsValidPassword                                                                                  */
        /* Purpose:     Ensures password is not empty and has minimum length requirement.                                 */
        /* Returns:     true if valid, otherwise false.                                                                    */
        public static bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            if (password.Length < 6)
                return false;

            return true;
        }

        /* Function:    SanitizeUsername                                                                                 */
        /* Purpose:     Trims whitespace and converts username to lowercase for consistency.                               */
        /* Returns:     Sanitized username string.                                                                         */
        public static string SanitizeUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return string.Empty;

            return username.Trim().ToLower();
        }
    }

    public static class PasswordHelper
    {
        /* Function:    CheckPasswordStrength                                                                            */
        /* Purpose:     Rates password as Weak / Medium / Strong based on character variety and length.                   */
        /* Returns:     Strength rating string.                                                                            */
        public static string CheckPasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return "Weak";

            int score = 0;

            if (password.Length >= 8)
                score++;

            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]"))
                score++;

            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]"))
                score++;

            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"\d"))
                score++;

            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[!@#$%^&*(),.?""':{}|<>]"))
                score++;

            if (score <= 2)
                return "Weak";
            else if (score <= 4)
                return "Medium";
            else
                return "Strong";
        }
    }
}
