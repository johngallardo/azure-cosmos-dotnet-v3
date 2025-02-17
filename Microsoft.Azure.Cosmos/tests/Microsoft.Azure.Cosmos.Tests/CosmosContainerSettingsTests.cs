﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosContainerSettingsTests
    {
        [TestMethod]
        public void DefaultIncludesPopulated()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            Assert.IsNotNull(containerSettings.IndexingPolicy);

            containerSettings.IndexingPolicy = new Cosmos.IndexingPolicy();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);

            // HAKC: Work-around till BE fixes defaults 
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(1, containerSettings.IndexingPolicy.IncludedPaths.Count);

            Cosmos.IncludedPath defaultEntry = containerSettings.IndexingPolicy.IncludedPaths[0];
            Assert.AreEqual(Cosmos.IndexingPolicy.DefaultPath, defaultEntry.Path);
            Assert.AreEqual(0, defaultEntry.Indexes.Count);
        }

        [TestMethod]
        public void ValidateSerialization()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            Stream basic = MockCosmosUtil.Serializer.ToStream<ContainerProperties>(containerSettings);
            ContainerProperties response = MockCosmosUtil.Serializer.FromStream<ContainerProperties>(basic);
            Assert.AreEqual(containerSettings.Id, response.Id);
            Assert.AreEqual(containerSettings.PartitionKeyPath, response.PartitionKeyPath);
        }

        [TestMethod]
        public void ValidateClientEncryptionPolicyDeserialization()
        {
            ClientEncryptionPolicy policy = MockCosmosUtil.Serializer.FromStream<ClientEncryptionPolicy>(new MemoryStream(
                Encoding.UTF8.GetBytes("{ 'policyFormatVersion': 2, 'newproperty': 'value'  }")));
            Assert.AreEqual(2, policy.PolicyFormatVersion);
        }

        [TestMethod]
        public void DefaultIncludesShouldNotBePopulated()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");
            Assert.IsNotNull(containerSettings.IndexingPolicy);

            // Any exclude path should not auto-generate default indexing
            containerSettings.IndexingPolicy = new Cosmos.IndexingPolicy();
            containerSettings.IndexingPolicy.ExcludedPaths.Add(new Cosmos.ExcludedPath() { Path = "/some" });

            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);

            // None indexing mode should not auto-generate the default indexing 
            containerSettings.IndexingPolicy = new Cosmos.IndexingPolicy
            {
                IndexingMode = Cosmos.IndexingMode.None
            };

            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
            containerSettings.ValidateRequiredProperties();
            Assert.AreEqual(0, containerSettings.IndexingPolicy.IncludedPaths.Count);
        }

        [TestMethod]
        public void DefaultSameAsDocumentCollection()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey");

            DocumentCollection dc = new DocumentCollection()
            {
                Id = "TestContainer",
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>()
                    {
                        "/partitionKey"
                    }
                }
            };
            CosmosContainerSettingsTests.AssertSerializedPayloads(containerSettings, dc);
        }

        [TestMethod]
        public void DefaultIndexingPolicySameAsDocumentCollection()
        {
            ContainerProperties containerSettings = new ContainerProperties("TestContainer", "/partitionKey")
            {
                IndexingPolicy = new Cosmos.IndexingPolicy()
            };

            DocumentCollection dc = new DocumentCollection()
            {
                Id = "TestContainer",
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>()
                    {
                        "/partitionKey"
                    }
                }
            };
            Documents.IndexingPolicy ip = dc.IndexingPolicy;

            CosmosContainerSettingsTests.AssertSerializedPayloads(containerSettings, dc);
        }

        private static string SerializeDocumentCollection(DocumentCollection collection)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                collection.SaveTo(ms);

                ms.Position = 0;
                using (StreamReader sr = new StreamReader(ms))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static void AssertSerializedPayloads(ContainerProperties settings, DocumentCollection documentCollection)
        {
            JsonSerializerSettings jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            // HAKC: Work-around till BE fixes defaults 
            settings.ValidateRequiredProperties();

            string containerSerialized = JsonConvert.SerializeObject(settings, jsonSettings);
            string collectionSerialized = CosmosContainerSettingsTests.SerializeDocumentCollection(documentCollection);

            JObject containerJObject = JObject.Parse(containerSerialized);
            JObject collectionJObject = JObject.Parse(collectionSerialized);

            Assert.AreEqual(JsonConvert.SerializeObject(OrderProeprties(collectionJObject)), JsonConvert.SerializeObject(OrderProeprties(containerJObject)));
        }

        private static JObject OrderProeprties(JObject input)
        {
            System.Collections.Generic.List<JProperty> props = input.Properties().ToList();
            foreach (JProperty prop in props)
            {
                prop.Remove();
            }

            foreach (JProperty prop in props.OrderBy(p => p.Name))
            {
                input.Add(prop);
                if (prop.Value is JObject)
                {
                    OrderProeprties((JObject)prop.Value);
                }
            }

            return input;
        }
    }
}
