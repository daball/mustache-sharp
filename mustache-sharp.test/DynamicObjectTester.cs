using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mustache;

namespace Mustache.Test
{
    /// <summary>
    /// Summary description for DynamicObjectTester
    /// </summary>
    [TestClass]
    public class DynamicObjectTester
    {
        public DynamicObjectTester()
        {
            //set desired test output without using Mustache
            this.desiredTestOutput = String.Format(testStringFormatTemplate, new object[] {
                testDynamicObject.Name,
                testDynamicObject.Tags[0],
                testDynamicObject.Tags[1],
                testDynamicObject.Address.Address1,
                testDynamicObject.Address.City
            });
        }

        private TestContext testContextInstance;

        private string testMustacheTemplate = "{{Name}} {{#each Tags}}{{this}} {{/each}}{{#with Address}}{{Address1}} {{City}}{{/with}}";
        private string testStringFormatTemplate = "{0} {1} {2} {3} {4}";
        private dynamic testDynamicObject = new {
                                                Name = "My Name",
                                                Tags = new string[]
                                                {
                                                    "Tag 1",
                                                    "Tag 2"
                                                },
                                                Address = new
                                                {
                                                    Address1 = "101 One Way",
                                                    City = "My City"
                                                }
                                            };
        private string desiredTestOutput = String.Empty;

        private FormatCompiler compiler = new FormatCompiler();

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestDynamic_Render()
        {
            Assert.AreEqual(desiredTestOutput, compiler.Compile(testMustacheTemplate).Render(testDynamicObject));
        }

        /// <summary>
        /// If we try to print a key that doesn't exist, an exception should be thrown.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void TestDynamic_KeyNotFoundException_WithDynamic()
        {
            dynamic d = new object();
            compiler.Compile(testMustacheTemplate).Render(d);
        }

        /// <summary>
        /// If we try to print a key that doesn't exist, we can provide a
        /// handler to provide a substitute.
        /// </summary>
        [TestMethod]
        public void TestDynamic_MissingKey_CallsKeyNotFoundHandler()
        {
            const string format = @"Hello, {{Name}}!!!";
            Generator generator = compiler.Compile(format);
            generator.KeyNotFound += (obj, args) =>
            {
                args.Substitute = "Unknown";
                args.Handled = true;
            };
            string actual = generator.Render(new object());
            string expected = "Hello, Unknown!!!";
            Assert.AreEqual(expected, actual, "The wrong message was generated.");
        }
    }
}
