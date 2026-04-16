#This is a local runner to prepare the azure environment,it creates the resource group and storage account to host the terraform state flies
# Variables
export ARM_SUBSCRIPTION_ID="86c326bd-934f-4e59-8f4a-8b41d78bda9c"
RESOURCE_GROUP="rg-vcbf-state"
LOCATION="uksouth"
#generate a unique name fot the storage account using the date and a string
STORAGE_ACCOUNT="stvcbf251125"   
CONTAINER_NAME="vcbf-state"

# Create the resource group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION

# Create the storage account
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2

# Get the storage account key
ACCOUNT_KEY=$(az storage account keys list \
  --resource-group $RESOURCE_GROUP \
  --account-name $STORAGE_ACCOUNT \
  --query "[0].value" -o tsv)

# Create the blob container
az storage container create \
  --name $CONTAINER_NAME \
  --account-name $STORAGE_ACCOUNT \
  --account-key $ACCOUNT_KEY

echo "Resource group: $RESOURCE_GROUP"
echo "Storage account: $STORAGE_ACCOUNT"
echo "Container: $CONTAINER_NAME"