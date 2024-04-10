subscriptionID=$(az account list --query "[?contains(name,'Microsoft')].[id]" -o tsv)
echo "Test subscription ID is = " $subscriptionID
az account set --subscription $subscriptionID
az account show

location=northeurope
postfix=$RANDOM

#----------------------------------------------------------------------------------
# Database infrastructure
#----------------------------------------------------------------------------------

export dbResourceGroup=cont-land-data$postfix
export dbServername=cont-land-sql$postfix
export dbPoolname=dbpool
export dbAdminlogin=FancyUser3
export dbAdminpassword=Sup3rStr0ng52$postfix
export dbPaperName=paperorders
export dbDeliveryName=deliveries

az group create --name $dbResourceGroup --location $location

az sql server create --resource-group $dbResourceGroup --name $dbServername --location $location \
--admin-user $dbAdminlogin --admin-password $dbAdminpassword
	
az sql elastic-pool create --resource-group $dbResourceGroup --server $dbServername --name $dbPoolname \
--edition Standard --dtu 50 --zone-redundant false --db-dtu-max 50

az sql db create --resource-group $dbResourceGroup --server $dbServername --elastic-pool $dbPoolname \
--name $dbPaperName --catalog-collation SQL_Latin1_General_CP1_CI_AS
	
az sql db create --resource-group $dbResourceGroup --server $dbServername --elastic-pool $dbPoolname \
--name $dbDeliveryName --catalog-collation SQL_Latin1_General_CP1_CI_AS	

sqlClientType=ado.net

SqlPaperString=$(az sql db show-connection-string --name $dbPaperName --server $dbServername --client $sqlClientType --output tsv)
SqlPaperString=${SqlPaperString/Password=<password>;}
SqlPaperString=${SqlPaperString/<username>/$dbAdminlogin}

SqlDeliveryString=$(az sql db show-connection-string --name $dbDeliveryName --server $dbServername --client $sqlClientType --output tsv)
SqlDeliveryString=${SqlDeliveryString/Password=<password>;}
SqlDeliveryString=${SqlDeliveryString/<username>/$dbAdminlogin}

SqlPaperPassword=$dbAdminpassword

#----------------------------------------------------------------------------------
# AKS infrastructure
#----------------------------------------------------------------------------------

location=northeurope
groupName=cont-land-cluster$postfix
clusterName=cont-land-cluster$postfix
registryName=contlandregistry$postfix


az group create --name $groupName --location $location

az acr create --resource-group $groupName --name $registryName --sku Standard
az acr identity assign --identities [system] --name $registryName

az aks create --resource-group $groupName --name $clusterName --node-count 3 --generate-ssh-keys --network-plugin azure
az aks update --resource-group $groupName --name $clusterName --attach-acr $registryName
az aks enable-addons --addon monitoring --name $clusterName --resource-group $groupName

#----------------------------------------------------------------------------------
# Service bus queue
#----------------------------------------------------------------------------------

groupName=cont-land-extras$postfix
location=northeurope
az group create --name $groupName --location $location
namespaceName=contLand$postfix
queueName=createdelivery

az servicebus namespace create --resource-group $groupName --name $namespaceName --location $location
az servicebus queue create --resource-group $groupName --name $queueName --namespace-name $namespaceName

serviceBusString=$(az servicebus namespace authorization-rule keys list --resource-group $groupName --namespace-name $namespaceName --name RootManageSharedAccessKey --query primaryConnectionString --output tsv)

#----------------------------------------------------------------------------------
# Application insights
#----------------------------------------------------------------------------------

insightsName=contLandlogs$postfix
az monitor app-insights component create --resource-group $groupName --app $insightsName --location $location --kind web --application-type web --retention-time 120

instrumentationKey=$(az monitor app-insights component show --resource-group $groupName --app $insightsName --query  "instrumentationKey" --output tsv)

#----------------------------------------------------------------------------------
# Azure Container Apps
#----------------------------------------------------------------------------------

instancesGroupName=cont-land-instances$postfix
location=northeurope
az group create --name $instancesGroupName --location $location

#----------------------------------------------------------------------------------
# Azure Container Apps
#----------------------------------------------------------------------------------

az extension add --name containerapp --upgrade

az provider register --namespace Microsoft.App

az provider register --namespace Microsoft.OperationalInsights

acaGroupName=cont-land-containerapp$postfix
location=northeurope
logAnalyticsWorkspace=cont-land-logs$postfix
containerAppsEnv=contl-environment$postfix

az group create --name $acaGroupName --location $location

az monitor log-analytics workspace create \
--resource-group $acaGroupName --workspace-name $logAnalyticsWorkspace

logAnalyticsWorkspaceClientId=`az monitor log-analytics workspace show --query customerId -g $acaGroupName -n $logAnalyticsWorkspace -o tsv | tr -d '[:space:]'`

logAnalyticsWorkspaceClientSecret=`az monitor log-analytics workspace get-shared-keys --query primarySharedKey -g $acaGroupName -n $logAnalyticsWorkspace -o tsv | tr -d '[:space:]'`

az containerapp env create \
--name $containerAppsEnv \
--resource-group $acaGroupName \
--logs-workspace-id $logAnalyticsWorkspaceClientId \
--logs-workspace-key $logAnalyticsWorkspaceClientSecret \
--dapr-instrumentation-key $instrumentationKey \
--logs-destination log-analytics \
--location $location

az containerapp env show --resource-group $acaGroupName --name $containerAppsEnv

# we don't need a section below for this workshop, but you can use it later
# use command below to fill credentials values if you want to use section below 
#az acr credential show --name $registryName 

#imageName=<CONTAINER_IMAGE_NAME>
#acrServer=<REGISTRY_SERVER>
#acrUser=<REGISTRY_USERNAME>
#acrPassword=<REGISTRY_PASSWORD>

#az containerapp create \
#  --name my-container-app \
#  --resource-group $acaGroupName \
#  --image $imageName \
#  --environment $containerAppsEnv \
#  --registry-server $acrServer \
#  --registry-username $acrUser \
#  --registry-password $acrPassword

#----------------------------------------------------------------------------------
# Azure Key Vault with secrets assignment and access setup
#----------------------------------------------------------------------------------

keyvaultName=cont-land$postfix
principalName=vaultadmin
principalCertName=vaultadmincert

az keyvault create --resource-group $groupName --name $keyvaultName --location $location
az keyvault secret set --name SqlPaperPassword --vault-name $keyvaultName --value $SqlPaperPassword

az ad sp create-for-rbac --name $principalName --create-cert --cert $principalCertName --keyvault $keyvaultName --skip-assignment --years 3

# get appId from output of step above and add it after --id in command below.

# az ad sp show --id 474f817c-7eba-4656-ae09-979a4bc8d844
# get object Id (located before info object) from command output above and set it to command below 

# az keyvault set-policy --name $keyvaultName --object-id f1d1a707-1356-4fb8-841b-98e1d9557b05 --secret-permissions get
#----------------------------------------------------------------------------------
# SQL connection strings
#----------------------------------------------------------------------------------

printf "\n\nRun string below in local cmd prompt to assign secret to environment variable SqlPaperString:\nsetx SqlPaperString \"$SqlPaperString\"\n\n"
printf "\n\nRun string below in local cmd prompt to assign secret to environment variable SqlDeliveryString:\nsetx SqlDeliveryString \"$SqlDeliveryString\"\n\n"
printf "\n\nRun string below in local cmd prompt to assign secret to environment variable SqlPaperPassword:\nsetx SqlPaperPassword \"$SqlPaperPassword\"\n\n"
printf "\n\nRun string below in local cmd prompt to assign secret to environment variable SqlDeliveryPassword:\nsetx SqlDeliveryPassword \"$SqlPaperPassword\"\n\n"
printf "\n\nRun string below in local cmd prompt to assign secret to environment variable ServiceBusString:\nsetx ServiceBusString \"$serviceBusString\"\n\n"

echo "Update open-telemetry-collector-appinsights.yaml in Step 5 End => <INSTRUMENTATION-KEY> value with:  " $instrumentationKey