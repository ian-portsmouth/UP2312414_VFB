# main resource group
resource "azurerm_resource_group" "rg" {
  name     = "${var.project_name}-rg"
  location = var.location

  tags = {
    project = var.project_name
    env     = "VFB"
  }
}

## Create a resource Group for the automation account
resource "azurerm_resource_group" "automation_rg" {
  name     = "${var.project_name}-automation-rg"
  location = var.location
  tags = {
    project = var.project_name
    env     = "VFB"
  }
}

