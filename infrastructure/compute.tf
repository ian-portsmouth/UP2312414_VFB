# Generate a random string
resource "random_string" "suffix" {
  length  = 6
  upper   = false
  special = false
}

# Deployment of windows VM web server
resource "azurerm_windows_virtual_machine" "vm" {
  name                = "vm-${var.project_name}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  size                = "Standard_D2s_v6"

  admin_username = var.admin_username
  admin_password = var.admin_password

  network_interface_ids = [azurerm_network_interface.nic.id]

  source_image_reference {
    publisher = "MicrosoftWindowsServer"
    offer     = "WindowsServer"
    sku       = "2022-datacenter-g2"
    version   = "latest"
  }

  os_disk {
    name                 = "${var.project_name}-osdisk"
    caching              = "ReadWrite"
    storage_account_type = "Standard_LRS"
  }

  computer_name  = "${var.project_name}-vm"
  secure_boot_enabled = true
  vtpm_enabled        = true

  tags = azurerm_resource_group.rg.tags
}


# Installation of IIS will be done using Azure Automation DSC



