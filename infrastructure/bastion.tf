
#deployment of bastion host 
resource "azurerm_bastion_host" "vm_bastion" {
  depends_on = [ azurerm_virtual_network.vnet ]
  name                = "${var.project_name}-bastion"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name

  ip_configuration {
    name                 = "configuration"
    subnet_id            = azurerm_subnet.bastion_subnet.id
    public_ip_address_id = azurerm_public_ip.bastion.id
  }
}



