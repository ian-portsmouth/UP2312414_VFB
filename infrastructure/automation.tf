# Create an  automation account
resource "azurerm_automation_account" "vfb" {
    depends_on = [ azurerm_windows_virtual_machine.vm ]
  name                = "${var.project_name}-account"
  location            = azurerm_resource_group.automation_rg.location
  resource_group_name = azurerm_resource_group.automation_rg.name
  sku_name            = "Basic"

 tags = {
    project = var.project_name
    env     = "VFB"
  }
}