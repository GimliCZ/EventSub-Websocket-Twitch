using System.Reflection;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Extensions;
using Twitch.EventSub.SubsRegister;
using Twitch.EventSub.SubsRegister.Models;
using Twitch.EventSub.User;


namespace TwitchEventSub_Websocket.Tests.SanityChecks
{
    public class RegistryTests
    {
        [Fact]
        public void TestRegistryKeyCountMatchesRegistryItemCount()
        {
            // Retrieve all constants from RegistryKeys
            var registryKeys = typeof(RegisterKeys)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(x => x.Name != nameof(RegisterKeys.KeysList))
                .Select(field => field.GetValue(null) as string)
                .ToList();

            // Retrieve all RegistryItems from Registry class
            var registryItems = typeof(Register)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(RegisterItem)) // Ensure the field is of type RegistryItem
            .Select(field => field.GetValue(null) as RegisterItem) // Get the value and cast it to RegistryItem
            .Where(item => item != null) // Filter out null values
            .ToList();

            // Ensure registryKeys and registryItems are not null
            Assert.NotNull(registryKeys);
            Assert.NotNull(registryItems);

            // Count of keys
            var registryKeyCount = registryKeys.Count;
            var registryItemCount = registryItems.Count;

            // Verify counts are the same
            Assert.Equal(registryKeyCount, registryItemCount);
        }

        [Fact]
        public void TestRegistryKeyCountMatchesEventProviderCount()
        {
            // Retrieve all constants from RegistryKeys
            var registryKeys = typeof(RegisterKeys)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(x => x.Name != nameof(RegisterKeys.KeysList))
                .Select(field => field.GetValue(null) as string)
                .ToList();

            // Retrieve all events from EventProvider
            var eventFields = typeof(EventProvider)
                        .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
                        .ToList();

            // Ensure registryKeys and registryItems are not null
            Assert.NotNull(registryKeys);

            // Count of keys
            var registryKeyCount = registryKeys.Count;
            var registryItemCount = eventFields.Count - 3; // 3 are additional functions

            // Verify counts are the same
            Assert.Equal(registryKeyCount, registryItemCount);
        }

        [Fact]
        public void TestRegistryKeyCountMatchesTypeVersionConditionMapCount()
        {
            // Retrieve all RegistryItems from Registry class
            var registryItems = typeof(Register)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(RegisterItem)) // Ensure the field is of type RegistryItem
            .Select(field => field.GetValue(null) as RegisterItem) // Get the value and cast it to RegistryItem
            .Where(item => item != null) // Filter out null values
            .ToList();

            // Retrieve all subscription types from TypeVersionConditionMap
            var subscriptionTypes = typeof(CreateSubscriptionRequestExtension)
                .GetField("TypeVersionConditionMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.GetValue(null) as Dictionary<SubscriptionTypes, (string Type, string Version, List<ConditionTypes> Conditions)>;

            // Ensure registryItems and subscriptionTypes are not null
            Assert.NotNull(registryItems);
            Assert.NotNull(subscriptionTypes);

            // Count of registry items
            var registryItemCount = registryItems.Count;
            var subscriptionTypeCount = subscriptionTypes.Count;

            // Verify counts are the same
            Assert.Equal(registryItemCount, subscriptionTypeCount);
        }

        [Fact]
        public void TestNoRegistryItemKeyIsNull()
        {
            // Retrieve all RegistryItems from Registry class
            var registryItems = typeof(Register)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(RegisterItem)) // Ensure the field is of type RegistryItem
            .Select(field => field.GetValue(null) as RegisterItem) // Get the value and cast it to RegistryItem
            .Where(item => item != null) // Filter out null values
            .ToList();

            // Ensure registryItems is not null
            Assert.NotNull(registryItems);

            // Check that none of the keys are null
            foreach (var item in registryItems)
            {
                Assert.NotNull(item);
                Assert.NotNull(item.Key);
            }
        }
    }
}