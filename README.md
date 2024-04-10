# azure-container-demo
From container apps to Kubernetes

## Prerequisites

1. Visual Studio or Visual Studio Code with .NET Framework 6.
2. Docker Desktop to run the containerized application locally.
https://www.docker.com/products/docker-desktop
3. DAPR CLI installed on a local machine.
https://docs.dapr.io/getting-started/install-dapr-cli/
4. AZ CLI tools installation(for cloud deployment)
https://aka.ms/installazurecliwindows
5. Azure subscription, if you want to deploy applications to Kubernetes(AKS).
https://azure.microsoft.com/en-us/free/
6. Kubectl installation https://kubernetes.io/docs/tasks/tools/install-kubectl-windows/#install-kubectl-binary-with-curl-on-windows
7. Good mood :)

## Steps
1. Step 1. Azure infrastructure
2. Step 2. Local containerisation
3. Step 3. Azure Container instances deploy
4. Step 4. Azure Container instances multi container group
5. Step 5. Azure Container Apps
6. Step 6. Container Apps with DAPR
7. Step 7. Migration to Azure Kubernetes Service and DAPR
8. Step 8. AKS Component switch and migration to on-premises.


## Step 1. Azure infrastructure
Script below should be run via Azure Portal bash console. 
You will receive database connection strings with setx command as output of this script. Along with Application Insights key
Please add a correct name of your subscription to the first row of the script. 

As result of this deployemt you should open the local command line as admin and execute output strings from the script execution to set environment variables.
It is also good to store them in the text file for the future usage

You might need to reboot your PC so secrets will be available from the OS.

For the start the preferrable way is to use Azue CLI bash console via Azure portal.

```bash
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
# Azure Container Instances
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
```

## Step 2. Local containerisation

First we adding docker containerization via context menu of each project.

<img width="489" alt="image" src="https://user-images.githubusercontent.com/36765741/204159578-5e72e255-928d-4b75-bd67-3b9f8a23e48f.png">

Then we adding orchestration support via docker compose again to the each project

If you decide to add storage at this step, then you should add the environment variable file to the root folder, so secrets will be shared between service for simplicity

<img width="183" alt="image" src="https://user-images.githubusercontent.com/36765741/204159631-754bfbfe-7052-4e8d-a286-c71347266586.png">

Then we changing order controller url for communication inside docker
```
            string url =
                $"http://tpaperorders:80/api/delivery/create/{savedOrder.ClientId}/{savedOrder.Id}/{savedOrder.ProductCode}/{savedOrder.Quantity}";
```
And from this point you should run solution in debug with docker compose option

![image](https://user-images.githubusercontent.com/36765741/204160258-35c356ff-931b-424c-9bac-6d261f432351.png)

!! Be aware, if you have docker build exceptions in Visual studio with errors related to the File system, there is a need to configure docker desktop. 
Open Docker desktop => configuration => Resources => File sharing => Add your project folder or entire drive, C:\ for example. Dont forget to remove drive setting later on.

!! When you try to start the same solution from the new folder, you might need to stop and delete containers via docker compose.

## Step 3. Azure Container instances deploy.

The first thing is we need to login locally to Azure and authenticate to the newly created Azure Container Registry, build, tag and push container there.

Then we will create identity for container registry.

And finally create a new container instance from our container in Azure Container Registry


Let's begin with local CMD promt and pushing of the container to Azure
!!!Use additional command az account set --subscription 95cd9078f8c to deploy resources into the correct subscription
```cmd
az login

az account show
az acr login --name contlandregistry
```

Open Visual studio and re-build your project in the Release mode, check with command line that the new container with the latest tag is created

Set a next version in manifest and command below before execution, check docker images command

```
docker tag tpaperorders:latest contlandregistry.azurecr.io/tpaperorders:v1
docker images
```

then push container to the container registry with
```
docker push contlandregistry.azurecr.io/tpaperorders:v1
```

Check if image is in the container registry

```
az acr repository list --name contlandregistry --output table
```

Then we are moving to the creation of service principal for Container registry via Azure Portal Bash console

```
registry=contlandregistry
principalName=registryPrincipal

registryId=$(az acr show --name $registry --query "id" --output tsv)

regPassword=$(az ad sp create-for-rbac --name $principalName --scopes $registryId --role acrpull --query "password" --output tsv)
regUser=$(az ad sp list --display-name $principalName --query "[].appId" --output tsv)

echo "Service principal ID: $regUser"
echo "Service principal password: $regPassword"
```

The output of the following script should containe login and password

```
Service principal ID: 277a0a62-9fb0
Service principal password: iUe44444444444444444444444a2r
```

Then we can continue from a local command line or azure portal.

Getting the login server

```
az acr show --name contlandregistry --query loginServer
```
 output will be contlandregistry.azurecr.io

So we adding the correct values to our application string
The resource group cont-land-instances with postfix created earlier.
--dns-name-label is your unique public name, so you can create it with your container registry name and postfix.

```
az container create --resource-group cont-land-instances --name cont-land-aci --image contlandregistry.azurecr.io/tpaperorders:v1 --cpu 1 --memory 1 --registry-login-server contlandregistry.azurecr.io --registry-username 277a0a62-9fb0 --registry-password iUe44444444444444444444444a2r --ip-address Public --dns-name-label contlandregistry --ports 80
```

As results we have our new container app deployed

<img width="638" alt="image" src="https://user-images.githubusercontent.com/36765741/208309681-04de647c-118e-4c94-a9a2-134b667c0778.png">

You can monitor deployment of container instance with
```
az container show --resource-group cont-land-instances --name cont-land-aci --query instanceView.state
```

Our application is not using Application insights, so we can check logs quickly via additional command

```
az container logs --resource-group cont-land-instances --name cont-land-aci
```

This way you will see that we have an error with the url referencing the delivery controller, so we can fix it with Container App fqdn

```
            string url =
                $"http://contlandregistry.northeurope.azurecontainer.io:80/api/delivery/create/{savedOrder.ClientId}/{savedOrder.Id}/{savedOrder.ProductCode}/{savedOrder.Quantity}";
```

Rebuild container, tag it with version 2 and deploy it again to the container apps

## Step 4. Azure Container instances multi container group.

This is a quite exotic case, because usage of a container with a sidecar or several services inside one container group without scale possibility is almost pointless.

But you should be aware about this possibilty, so you can leverage simple two service scenario as fast and easy as possible.

At the moment you can safely skip this step and move to the container instances :).

let's login to our container registry from a step 4 folder
```cmd
az login

az account show
az acr login --name contlandregistry
```

We need to build, tag and push our container images to Container registry

```
docker tag tpaperorders:latest contlandregistry.azurecr.io/tpaperorders:v2
docker images
docker push contlandregistry.azurecr.io/tpaperorders:v2

docker tag tpaperdelivery:latest contlandregistry.azurecr.io/tpaperdelivery:v2
docker images
docker push contlandregistry.azurecr.io/tpaperdelivery:v2
```

Not it is time to authenticat docker to your azure subscription and create context for resource group from a command line inside step 4 solution folder

```
docker login azure
docker context create aci instancescontext
docker context ls
docker context use instancescontext

```

![image](https://user-images.githubusercontent.com/36765741/208311730-3623ac6c-0265-4da0-822f-4b725a364f05.png)

and after context set we can do the compose update

```
docker compose up
```

For extra details you can this refence 
https://learn.microsoft.com/en-us/azure/container-instances/tutorial-docker-compose



## Step 5. Azure Container Apps

As the initial step we will update our application with SQL database code, please double check difference between Step 5 initial commit and database update commit
Don't forget to add database secrets to the local.env secret file located in the root directory.

Then we will publish our project to the container apps environment via visual studio. You can also setup deployment with GitHub actions.

Deployment to Container Apps via Visual studio
Configuration for GitHub actions deployments

![image](https://user-images.githubusercontent.com/36765741/202031699-2787a2b0-2368-45e5-b37c-76e1550b78d7.png)

![image](https://user-images.githubusercontent.com/36765741/202032521-3797b950-d41f-4b43-8055-70ca53c3fd70.png)

![image](https://user-images.githubusercontent.com/36765741/202032630-01472083-e44d-4026-8e23-1bf2f82b6dfd.png)

![image](https://user-images.githubusercontent.com/36765741/202032687-b1cda810-8675-4120-9b80-28c0fc943087.png)

![image](https://user-images.githubusercontent.com/36765741/202032789-d6b845a6-9608-4b66-878b-6bea22fab75e.png)

![image](https://user-images.githubusercontent.com/36765741/202032840-2d215c00-cb11-402a-8983-56bffb511add.png)

And after a brief look with can figure out that deployment has failed
![image](https://user-images.githubusercontent.com/36765741/202293909-ab636822-5c15-4537-b58d-18c35957e911.png)

after a click on the failed deployment, we need to select Console logs and see results in the log analytics

After expanding the latest log entry, we can see Unhandled exception. System.ArgumentNullException: Value cannot be null. (Parameter 'Password')
Which means that we not configured secrets for our application.

Let's do a quick fix by adding secrets to the Container App settings (KeyVault we will use later)
![image](https://user-images.githubusercontent.com/36765741/202296088-7dc6f0cb-538a-4e2a-a459-5151c2038a01.png)

After this changes we re-deploying our application and getting another error Cannot open server 'cont-land-sql' requested by the login. Client with IP address

There is a two ways to solve this problem, the first is to use Azure Connector preview and make a direct link to database with secrets managed by KeyVault, or add IP address to exceptions. Or you can create a Container app environment with VNet from the start and use network endpoint of Azure SQL

After this changes our application successfuly provisioned.

There is a limit of one ingress per one container app, along with the reccomendation to host two containers only in case of workload + sidecar.
Moreover Visual studion will not let you easily do so.

So we need to deploy a delivery app as a separate app and configure urls for service to service communications.

We will need to get a full URL of Delivery Container APP from a portal or Azure cli for automation
```
acaGroupName=cont-land-containerapp
acaName=cont-land-containerapp
								  
az containerapp show --resource-group acaGroupName \
--name acaName --query properties.configuration.ingress.fqdn
```
so we will get following url

```
tpaperdelivery-app-2022111622390--dgcp1or.agreeablecoast-99a44d4d.northeurope.azurecontainerapps.io
```
and add internal, so it will look like
```
tpaperdelivery-app-2022111622390--dgcp1or.internal.agreeablecoast-99a44d4d.northeurope.azurecontainerapps.io
```
and add this value to DeliveryUrl environment variable file with docker url, and to container app config DeliveryUrl

And please allow Insecure connections for Delivery service via ingress configuration
![image](https://user-images.githubusercontent.com/36765741/202306805-5620b8cd-4fb1-4dba-80b7-8ce54e80dbc9.png)

We should use the full path to make initial call to order API and see results
tpaperorders-app-20221115224238--k1osno8.agreeablecoast-99a44d4d.northeurope.azurecontainerapps.io/api/order/create/1


## Step 6. Container Apps with DAPR

And initialize DAPR for the local development

```
dapr init
```
We can check results(container list) by opening Docker Desktop or running command docker ps.

Lets update our solution compose file with DAPR sidecar containers, for production you should replace DAPR latest with particular version to avoid problems with auto updates.

```
version: '3.4'

services:
  tpaperdelivery:
    image: ${DOCKER_REGISTRY-}tpaperdelivery
    build:
      context: .
      dockerfile: TPaperDelivery/Dockerfile
    ports:
      - "52000:50001"
    env_file:
      - settings.env
      
  tpaperdelivery-dapr:
    image: "daprio/daprd:latest"
    command: [ "./daprd", "-app-id", "tpaperdelivery", "-app-port", "80" ]
    depends_on:
      - tpaperdelivery
    network_mode: "service:tpaperdelivery"
      
  tpaperorders:
    image: ${DOCKER_REGISTRY-}tpaperorders
    build:
      context: .
      dockerfile: TPaperOrders/Dockerfile
    ports:
      - "51000:50001"
    env_file:
      - settings.env

  tpaperorders-dapr:
    image: "daprio/daprd:latest"
    command: [ "./daprd", "-app-id", "tpaperorders", "-app-port", "80" ]
    depends_on:
      - tpaperorders
    network_mode: "service:tpaperorders"      
```

Now we need to make adjustments to PaperOrders project, by adding DAPR dependency

```
<PackageReference Include="Dapr.AspNetCore" Version="1.9.0" />
```
Adding it to the Startup 

```
services.AddControllers().AddDapr();
```
And replacing method CreateDeliveryForOrder with http endpoint invocation via DAPR client

```
        private async Task<DeliveryModel> CreateDeliveryForOrder(EdiOrder savedOrder, CancellationToken cts)
        {
            string serviceName = "tpaperdelivery";
            string route = $"api/delivery/create/{savedOrder.ClientId}/{savedOrder.Id}/{savedOrder.ProductCode}/{savedOrder.Quantity}";

            DeliveryModel savedDelivery = await _daprClient.InvokeMethodAsync<DeliveryModel>(
                                          HttpMethod.Get, serviceName, route, cts);

            return savedDelivery;
        }
        }
```

If we will start a service and invoke a new order via http://localhost:52043/api/order/create/1 we can see that everything working as usual, except that we got additional container sidecars for each service 

This way we leveraged service locator provided by dapr, it is still a http communication between services, but now you can skip usage of evnironment variable and routing will be a responsibility of DAPR.

### Now we will need to add a PubSub component and DAPR component 

First we preparing simplified DAPR pubsub yaml manifest for pubsub(available in yaml folder of Step 2 End)

```
componentType: pubsub.azure.servicebus
version: v1
metadata:
- name: connectionString
  secretRef: sbus-connectionstring
secrets:
- name: sbus-connectionstring
  value: super-secret
```

And then deploying it to Azure via local azure CLI or portal console with file upload. Locally you should do az login first. 
The pubsub component would be pubsubsbus, we will use it later in code.
```
az containerapp env dapr-component set --resource-group cont-land-containerapp --name contl-environment --dapr-component-name pubsubsbus --yaml "pubsubsbus.yaml"
```

Important thing, we are adding this DAPR component for entire environment, so it will be available for all apps, also we not included scopes at this step, something for the future.

Afterwards there is a need to put the correct Azure Service Bus connection string on the portal via Container App environment
![image](https://user-images.githubusercontent.com/36765741/202546502-342421d4-c14f-4f2f-8118-df220f752232.png)

One important this, please add the following section to your service bus connection string ";EntityPath=createdelivery"
"Endpoint=sb://contland2141.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=CGnGz1L+Jw=;EntityPath=createdelivery"

Now let's add DAPR pub/sub components to our solution.

Change CreateDeliveryForOrder in TPaperOrders project to the following code that uses DAPR pubsub
```
        private async Task<DeliveryModel> CreateDeliveryForOrder(EdiOrder savedOrder, CancellationToken cts)
        {
            var newDelivery = new DeliveryModel
            {
                Id = 0,
                ClientId = savedOrder.ClientId,
                EdiOrderId = savedOrder.Id,
                Number = savedOrder.Quantity,
                ProductId = 0,
                ProductCode = savedOrder.ProductCode,
                Notes = "Prepared for shipment"
            };

            await _daprClient.PublishEventAsync<DeliveryModel>("pubsub", "createdelivery", newDelivery, cts);

            return newDelivery;
        }
```

Adding DAPR dependency
```
 <PackageReference Include="Dapr.AspNetCore" Version="1.9.0" />
```

Change method CreateDeliveryForOrder in OrderController to
```
        private async Task<DeliveryModel> CreateDeliveryForOrder(EdiOrder savedOrder, CancellationToken cts)
        {
            var newDelivery = new DeliveryModel
            {
                Id = 0,
                ClientId = savedOrder.ClientId,
                EdiOrderId = savedOrder.Id,
                Number = savedOrder.Quantity,
                ProductId = 0,
                ProductCode = savedOrder.ProductCode,
                Notes = "Prepared for shipment"
            };

            await _daprClient.PublishEventAsync<DeliveryModel>("pubsubsbus", "createdelivery", newDelivery, cts);

            return newDelivery;
        }
```

And most importantly change double to decimal in Delivery model Number field


For TPaperDelivery project, update Startup class
```
services.AddControllers().AddDapr();
```
and 
```
app.UseAuthorization();

app.UseCloudEvents();

app.UseOpenApi();
app.UseSwaggerUi3();

app.UseEndpoints(endpoints =>
{
endpoints.MapSubscribeHandler();
endpoints.MapControllers();
});
```

And finally make a proper signature on ProcessEdiOrder endpoint
```
[Topic("pubsubsbus", "createdelivery")]
[HttpPost]
[Route("createdelivery")]
```

And add method to enumerate all stored deliveries
```
[HttpGet]
[Route("deliveries")]
public async Task<IActionResult> Get(CancellationToken cts)
{
    Delivery[] registeredDeliveries = await _context.Delivery.ToArrayAsync(cts);

    return new OkObjectResult(registeredDeliveries);
}
```

And after all this changes we can deploye and observe results in Azure.


## Step 7. Migration to Azure Kubernetes Service and DAPR

As our application grows, Container Apps might be not enough, so the essential migration path is to Azure Kubernetes Service

Spoiler. We will add Azure KeyVault in the next step and continue with abstracting it with DAPR component

First we need to add two secrets to the K8s manifest
Service bus connection string and SQL server password

We will need to encode secrets with base64 via command line or online tool https://string-functions.com/base64encode.aspx

You can check example conversion below
```
Endpoint=sb://contland.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=J+Jw=;EntityPath=createdelivery
RcG9eQ
```

And so our final manifest will look like this.
```
apiVersion: v1
kind: Secret
metadata:
  name: sbus-secret
type: Opaque
data:
  connectionstring: RW5kcG9mVyeQ==
---
apiVersion: v1
kind: Secret
metadata:
  name: sql-secret
type: Opaque
data:
  password: U3Vg==
```

Lets start with CMD. !!!Use additional command az account set --subscription 95cd9078f8c to deploy resources into the correct subscription
```cmd
az login

az account show
az acr login --name contlandregistry
az aks get-credentials --resource-group cont-land-cluster --name cont-land-cluster
kubectl config use-context cont-land-cluster
kubectl get all --all-namespaces
kubectl get ds ama-logs --namespace=kube-system
```

And initialize DAPR in our kubernetes cluster
```cmd
dapr init -k 
```

Validate results of initialization
```cmd
dapr status -k 
```
Then we will need to build our solution in release mode and observe results with command. We building containers trough Visual Studio and tagging them via command line
You can also start docker desktop application for GUI container handling.

Lets see what images do we have with
```cmd
docker images
```

Lets tag our newly built container with azure container registry name and version.
```cmd
docker tag tpaperorders:latest contlandregistry.azurecr.io/tpaperorders:v1
docker tag tpaperdelivery:latest contlandregistry.azurecr.io/tpaperdelivery:v1
```

Check results with
```cmd
docker images
```


And push images to container registry
```cmd
docker push contlandregistry.azurecr.io/tpaperorders:v1
docker push contlandregistry.azurecr.io/tpaperdelivery:v1
```

Now we need to update container version in orders manifest
```
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tpaperorders
  namespace: tpaper
  labels:
    app: tpaperorders
spec:
  replicas: 1
  selector:
    matchLabels:
      service: tpaperorders
  template:
    metadata:
      labels:
        app: tpaperorders
        service: tpaperorders
      annotations:
        dapr.io/enabled: "true"
        dapr.io/app-id: "tpaperorders"
        dapr.io/app-port: "80"
        dapr.io/log-level: debug
    spec:
      containers:
        - name: tpaperorders
          image: msactionregistry.azurecr.io/tpaperorders:v1
          imagePullPolicy: Always
          ports:
            - containerPort: 80
              protocol: TCP
          env:
            - name: ASPNETCORE_URLS
              value: http://+:80
            - name: SqlPaperString
              value: Server=tcp:cont-land-sql.database.windows.net,1433;Database=paperorders;User ID=FancyUser3;Encrypt=true;Connection Timeout=30;
            - name: SqlPaperPassword
              valueFrom:
                secretKeyRef:
                  name: sql-secret
                  key: password
---
apiVersion: v1
kind: Service
metadata:
  name: tpaperorders
  namespace: tpaper
  labels:
    app: tpaperorders
    service: tpaperorders
spec:
  type: LoadBalancer
  ports:
    - port: 80
      targetPort: 80
      protocol: TCP
  selector:
    service: tpaperorders
```

And delivery manifest
```
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tpaperdelivery
  namespace: tpaper
  labels:
    app: tpaperdelivery
spec:
  replicas: 1
  selector:
    matchLabels:
      service: tpaperdelivery
  template:
    metadata:
      labels:
        app: tpaperdelivery
        service: tpaperdelivery
      annotations:
        dapr.io/enabled: "true"
        dapr.io/app-id: "tpaperdelivery"
        dapr.io/app-port: "80"
        dapr.io/log-level: debug
    spec:
      containers:
        - name: tpaperdelivery
          image: contlandregistry.azurecr.io/tpaperdelivery:v1
          imagePullPolicy: Always
          ports:
            - containerPort: 80
              protocol: TCP
          env:
            - name: ASPNETCORE_URLS
              value: http://+:80
            - name: SqlDeliveryString
              value: Server=tcp:cont-land-sql.database.windows.net,1433;Database=deliveries;User ID=FancyUser3;Encrypt=true;Connection Timeout=30;
            - name: SqlPaperPassword
              valueFrom:
                secretKeyRef:
                  name: sql-secret
                  key: password
---
apiVersion: v1
kind: Service
metadata:
  name: tpaperdelivery
  namespace: tpaper
  labels:
    app: tpaperdelivery
    service: tpaperdelivery
spec:
  type: LoadBalancer
  ports:
    - port: 80
      targetPort: 80
      protocol: TCP
  selector:
    service: tpaperdelivery
```

But before deployment of our services we need to deploye secrets and pubsub DAPR component
As you can see, the file is slightly different from Container Apps version

DAPR pubsub component
```
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub-super-new
  namespace: tpaper
spec:
  type: pubsub.azure.servicebus
  version: v1
  metadata:
  - name: connectionString
    secretKeyRef:
      name: sbus-secret
      key:  connectionstring
auth:
  secretStore: kubernetes
scopes:
  - tpaperorders
  - tpaperdeliver
```

Manifest to create a new namespace
```
apiVersion: v1
kind: Namespace
metadata:
  name: tpaper
  labels:
    name: tpaper
```

creation of the new namespace, so we can easily find our services
```cmd
kubectl apply -f aks_namespace-tpaper.yaml
```

deployment of secrets and pubsub component
```cmd
kubectl apply -f aks_secrets.yaml
kubectl apply -f aks_pubsub-servicebus.yaml
```
Now we need to pray the "demo gods" for our deployment and run commands below
```cmd
kubectl apply -f aks_tpaperorders-deploy.yaml
kubectl apply -f aks_tpaperdelivery-deploy.yaml
```


You can use set of commands below for quick container/publish re-deployments.
Just change version in kubernetes manifest and commands below.
```cmd
docker tag tpaperorders:latest contlandregistry.azurecr.io/tpaperorders:v1
docker images
docker push contlandregistry.azurecr.io/tpaperorders:v1
kubectl apply -f aks_tpaperorders-deploy.yaml
kubectl get all --all-namespaces

docker tag tpaperdelivery:latest contlandregistry.azurecr.io/tpaperdelivery:v1
docker images
docker push contlandregistry.azurecr.io/tpaperdelivery:v1
kubectl apply -f aks_tpaperdelivery-deploy.yaml
kubectl get all --all-namespaces
```

And now the most interesting part. Is to debug our application and make it work the proper way.
For this we can use console and kubernetes container logs or Lens, The kubernetes IDE.

Let's check output of kubectl get all to get IP addresses of our services
```
20.82.168.123/api/order/create/1
20.82.168.61/api/delivery/deliveries
```

Nothing works and no one knows why (c)

Adding here commands to watch container logs for reference, but for better understanding we will use Lens.
You should get correct pod names from get all command and change log command accordingly.

```cmd
kubectl get all --all-namespaces

kubectl logs tpaperdelivery-599b8cd4b7-8nxzz daprd
kubectl logs tpaperdelivery-599b8cd4b7-8nxzz tpaperdelivery
```

![image](https://user-images.githubusercontent.com/36765741/203056933-9d0a8654-3138-4552-a084-f2b67e3fc753.png)

Our containers crushed, so we need to figure this out :)

let's procced with cmd and kubectl

switching to the tpaper namespace
```
kubectl config set-context --current --namespace=tpaper
kubectl get all
```
now we can get detailed information about failed pod
```
kubectl describe pod/tpaperdelivery-7698f99cd5-2q9ml
```
It will be a lot of information, but if there are no exact details, we should check our service manifest and connected dependencies, like secrets

Quick check of our namespace tpaper via Lens show us that there are no secrets configured
![image](https://user-images.githubusercontent.com/36765741/203061153-c903aa60-2e5e-4d01-a177-01f63dd10088.png)

And secrets configured for default namespace
![image](https://user-images.githubusercontent.com/36765741/203061285-a4ba0072-f55b-4401-8201-be9aa8fa22fa.png)

So we need to add namespace to our secret yaml
```
metadata:
  name: sbus-secret
  namespace: tpaper
type: Opaque
```
and deploy it again
```
kubectl apply -f aks_secrets.yaml
```

And then you can redeploy your containers, or simply restart deployment from Lens UI
![image](https://user-images.githubusercontent.com/36765741/203068399-9c4de228-27a0-4942-9764-52c9721bbb31.png)



And now it is much better, we have internal server exception with database migration and connectivity to SQL server
![image](https://user-images.githubusercontent.com/36765741/203067924-e35fb263-c86d-4588-ae73-b415c670e514.png)

Let's fix it

There are two steps to do it via Azure Portal.

Kubernetes connectivity
* Navigate into resource group MC_cont-land-cluster_cont-land-cluster_northeurope
* Open Virtual network there
* Open Service endpoints and click add
* Select Microsoft.SQL from dropdown and select aks-vnet in the next dropdown.
* Add additional integration with Microsoft.ServiceBus
* Save it, update might take a few minutes

SQL Server connectivity.
* Navigate to the resource group - ms-action-dapr-data
* Open Sql Server ms-action-dapr
* Click Show Firewall
* On top click add client IP address, so you can access sql server from your work machine
* Click  Add existing virtual network + Create new virtual network
* Add aks-vnet with a proper name(check name via AKS cluster group)
* Most important step - click Save in the portal UI

Now it is much better
![image](https://user-images.githubusercontent.com/36765741/203074200-17d114a2-deb9-4486-839d-d72ef74dd5aa.png)

Now we can restart the order service, and if we will do kubectl describe command, we can see that deployment yaml have wrong container registry name.
![image](https://user-images.githubusercontent.com/36765741/203076456-2da93355-cb8d-4d94-813c-03bb8b7a5910.png)

So we fixing the name in aks_tpaperorders-deploy.yaml from 
```
        - name: tpaperorders
          image: contlandregistry.azurecr.io/tpaperorders:v1
          imagePullPolicy: Always
```
And applying deployment again via cmd
```
kubectl apply -f aks_tpaperorders-deploy.yaml
kubectl get all 
```

And quick invocation of delivery endpoint shows that everything is working
![image](https://user-images.githubusercontent.com/36765741/203077131-345ee0ca-aa17-4ae2-9275-d2c27e918e54.png)

But when we try to create orders, we will get an error, no pubsub configured
![image](https://user-images.githubusercontent.com/36765741/203077605-e8092b15-4871-4320-8179-17402e359231.png)

The reason is that pubsub component in AKS have a different name in manifest, so we need to update it in Visual Studio and deploy services with V2 manifest update
Let's change pubsub component name from pubsubsbus to pubsub-super-new, change topic name from createdelivery to aksdelivery, rebuild containers in VS, tag them with version 2, change manifest and deploy everything

```cmd
docker tag tpaperorders:latest contlandregistry.azurecr.io/tpaperorders:v3
docker images
docker push contlandregistry.azurecr.io/tpaperorders:v3
kubectl apply -f aks_tpaperorders-deploy.yaml
kubectl get all --all-namespaces

docker tag tpaperdelivery:latest contlandregistry.azurecr.io/tpaperdelivery:v6
docker images
docker push contlandregistry.azurecr.io/tpaperdelivery:v6
kubectl apply -f aks_tpaperdelivery-deploy.yaml
kubectl get all --all-namespaces
```

Another possible error that you can encounter is the Error Connecting to subchannel, it means that something is wrong with DAPR sidecar
![image](https://user-images.githubusercontent.com/36765741/203084539-6fef1211-d25a-486e-a2b9-de282e0cf7e9.png)

It means that DAPR sidecar container was not deployed with your service, you can check it via Lena or console like this
![image](https://user-images.githubusercontent.com/36765741/203087594-9e39f234-7159-48c6-946c-7315147c5c76.png)
 or
![image](https://user-images.githubusercontent.com/36765741/203087760-20d536da-7b29-4dd5-bda8-9f6d8d703d7c.png)

If you are closely following this tutorial by yourself, there is a mistake in one of the manifests, and it can be easily fixed if you know about service discovery process.

! Make sure that dapr pubsub component deployed into the same namespace as services.
! Make sure that you changed service bus topic name in Visual Studio


## Step 8. AKS Component switch and migration to on-premises.

In case your session to Azure or container registry is timed out, please login again.

```bash
az login

az account show
az acr login --name contlandregistry
kubectl config use-context cont-land-cluster
kubectl config set-context --current --namespace=tpaper
kubectl get all
```

```bash
kubectl apply -f rabbitmq.yaml
kubectl apply -f aks_pubsub-rabbitmq.yaml
```

```cmd
docker tag tpaperorders:latest contlandregistry.azurecr.io/tpaperorders:v9
docker images
docker push contlandregistry.azurecr.io/tpaperorders:v9
kubectl apply -f aks_tpaperorders-deploy.yaml
kubectl get all

docker tag tpaperdelivery:latest contlandregistry.azurecr.io/tpaperdelivery:v9
docker images
docker push contlandregistry.azurecr.io/tpaperdelivery:v9
kubectl apply -f aks_tpaperdelivery-deploy.yaml
kubectl get all
```


Also we will add Application insights instrumentation for our project
```
    <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.21.0" />
    <PackageReference Include="Microsoft.ApplicationInsights.Kubernetes" Version="3.1.0" />
```
and configure it the old way with instrumentation key, you can also do this with connection string
```
            services.AddApplicationInsightsTelemetry("e2-7b799ab67b89");
            services.AddApplicationInsightsKubernetesEnricher();
```
then we need to rebuild solution and deploy updates

The section below can be used for logging outside of Azure platform
```bash
kubectl apply -f otel-collector-conf.yaml
kubectl apply -f collector-config.yaml
```

```cmd
docker tag tpaperorders:latest contlandregistry.azurecr.io/tpaperorders:v12
docker images
docker push contlandregistry.azurecr.io/tpaperorders:v12
kubectl apply -f aks_tpaperorders-deploy.yaml
kubectl get all

docker tag tpaperdelivery:latest contlandregistry.azurecr.io/tpaperdelivery:v12
docker images
docker push contlandregistry.azurecr.io/tpaperdelivery:v12
kubectl apply -f aks_tpaperdelivery-deploy.yaml
kubectl get all
```

 We intentionally skipped RabbitMQ local host setup, and this activity can be done via extra task
 By intalling and running the local container
 ```
 docker run -d --hostname my-rabbit --name some-rabbit rabbitmq:3
 ```
