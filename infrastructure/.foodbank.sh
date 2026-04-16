#set subscription

export ARM_SUBSCRIPTION_ID="86c326bd-934f-4e59-8f4a-8b41d78bda9c"
#set the application / environment 

export TF_VAR_application_name="foodbank"
export TF_VAR_org_name="victorycare"

#set the backend

export BACKEND_RESOURCE_GROUP="rg-vcbf-state"
export BACKEND_STORAGE_ACCOUNT="stvcbf251125"
export BACKEND_CONTAINER_NAME="vcbf-state"
export BACKEND_KEY=$TF_VAR_application_name-$TF_VAR_org_name

#run terraform

terraform init \
    -backend-config="resource_group_name=${BACKEND_RESOURCE_GROUP}" \
    -backend-config="storage_account_name=${BACKEND_STORAGE_ACCOUNT}" \
    -backend-config="container_name=${BACKEND_CONTAINER_NAME}" \
    -backend-config="key=${BACKEND_KEY}" 

terraform validate

terraform $* -var-file="./pass.tfvars"

rm -rf .terraform 