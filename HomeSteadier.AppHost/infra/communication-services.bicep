// Azure Communication Services + Email, provisioned as raw Bicep because there is no Aspire
// hosting integration and no Azure.Provisioning.CommunicationServices CDK package to build the
// resources with typed constructs (checked against Aspire 13.4.6 / Azure.Provisioning 1.5.0).
//
// Requires the Microsoft.Communication resource provider to be registered on the subscription:
//   az provider register --namespace Microsoft.Communication --wait
// Without it, provisioning fails with a MissingSubscriptionRegistration error.

@description('Name of the Email Communication Service resource.')
param emailServiceName string

@description('Name of the Communication Services resource.')
param communicationServiceName string

@description('Where message content and related data are stored at rest. Cannot be changed after creation.')
param dataLocation string = 'United States'

// Deliberately NOT named 'keyVaultName': that is one of AzureBicepResource.KnownParameters, which
// the host fills in automatically for the obsolete GetSecretOutput flow. Using a distinct name
// keeps this template bound to the vault AppHost passes in explicitly. Also avoids 'secret' in
// the name, which trips the secure-secrets-in-params linter on what is only a resource name.
@description('Name of the key vault that receives the ACS connection string. Passed in from AppHost.')
param vaultName string

// azd passes 'location' into every module it generates, so this must be declared even though
// Microsoft.Communication resources are global-only and can't use it — without it, compilation
// fails with BCP037 ("the property 'location' is not allowed on objects of type 'params'").
#disable-next-line no-unused-params
param location string = ''

// Every Microsoft.Communication resource is a global resource — 'global' is the only accepted
// location value. Data residency is controlled by dataLocation, not location.
var communicationLocation = 'global'

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: emailServiceName
  location: communicationLocation
  properties: {
    dataLocation: dataLocation
  }
}

// 'AzureManagedDomain' is a required magic name for the free azurecomm.net subdomain — Azure
// verifies it automatically, so no DNS records and no second deploy pass. The trade-off is a
// hard send quota (~100 messages/day, 10/minute) and a DoNotReply@<guid>.azurecomm.net sender
// that many providers treat as spam. Swap to a CustomerManagedDomain before real users rely on
// password reset.
resource azureManagedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: communicationLocation
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationServiceName
  location: communicationLocation
  properties: {
    dataLocation: dataLocation
    // Linking at creation is what lets the service send as this domain. The managed domain is
    // already verified, so there's no pending-verification state to wait on.
    linkedDomains: [
      azureManagedDomain.id
    ]
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: vaultName
}

// The connection string carries an access key, so it goes to the key vault rather than a plain
// deployment output (outputs are readable from deployment history).
resource connectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'connectionString'
  properties: {
    value: communicationService.listKeys().primaryConnectionString
  }
}

// Not a secret — just the address the managed domain will accept mail from.
output senderAddress string = 'DoNotReply@${azureManagedDomain.properties.fromSenderDomain}'
