# Main Vnet
resource "azurerm_virtual_network" "vnet" {
  name                = "${var.project_name}-vnet"
  address_space       = [var.vnet_prefix]
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  tags                = azurerm_resource_group.rg.tags
}

resource "azurerm_subnet" "subnet" {
  name                 = "${var.project_name}-subnet-web"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = [var.snet_prefix]

  service_endpoints = ["Microsoft.Sql"]
}

resource "azurerm_subnet" "bastion_subnet" {
  depends_on = [ azurerm_virtual_network.vnet ]
  name                 = "AzureBastionSubnet"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = [var.bastion_prefix]
}

resource "azurerm_public_ip" "bastion" {
  depends_on = [ azurerm_subnet.bastion_subnet ]
  name                = "bastion-pip"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  allocation_method   = "Static"
  sku                 = "Standard"
}

# NSG for AzureBastionSubnet
resource "azurerm_network_security_group" "bastion_subnet_nsg" {
  name                = "AzureBastionSubnet-nsg"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
}

#Inbound NSG Rules

resource "azurerm_network_security_rule" "bastion_in_443_internet" {
  name                        = "AllowHttpsInbound"
  priority                    = 120
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = "Internet"
  source_port_range           = "*"
  destination_address_prefix  = "*"
  destination_port_range      = "443"
  resource_group_name         = azurerm_resource_group.rg.name
  network_security_group_name = azurerm_network_security_group.bastion_subnet_nsg.name
}

resource "azurerm_network_security_rule" "bastion_in_443_gatewaymanager" {
  name                        = "AllowGatewayManagerInbound"
  priority                    = 130
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = "GatewayManager"
  source_port_range           = "*"
  destination_address_prefix  = "*"
  destination_port_range      = "443"
  resource_group_name         = azurerm_resource_group.rg.name
  network_security_group_name = azurerm_network_security_group.bastion_subnet_nsg.name
}

resource "azurerm_network_security_rule" "bastion_in_443_azlb" {
  name                        = "AllowAzureLoadBalancerInbound"
  priority                    = 140
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = "AzureLoadBalancer"
  source_port_range           = "*"
  destination_address_prefix  = "*"
  destination_port_range      = "443"
  resource_group_name         = azurerm_resource_group.rg.name
  network_security_group_name = azurerm_network_security_group.bastion_subnet_nsg.name
}

resource "azurerm_network_security_rule" "bastion_in_internal" {
  name                        = "AllowBastionHostCommunication"
  priority                    = 150
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "*" # "Any"
  source_address_prefix       = "VirtualNetwork"
  source_port_range           = "*"
  destination_address_prefix  = "VirtualNetwork"
  destination_port_ranges     = ["8080", "5701"]
  resource_group_name         = azurerm_resource_group.rg.name
  network_security_group_name = azurerm_network_security_group.bastion_subnet_nsg.name
}

# Outbound NSG Rules 

resource "azurerm_network_security_rule" "bastion_out_ssh_rdp" {
  name                        = "AllowSshRdpOutbound"
  priority                    = 100
  direction                   = "Outbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = "*"
  source_port_range           = "*"
  destination_address_prefix  = "VirtualNetwork"
  destination_port_ranges     = ["22", "3389"]
  resource_group_name         = azurerm_resource_group.rg.name
  network_security_group_name = azurerm_network_security_group.bastion_subnet_nsg.name
}

resource "azurerm_network_security_rule" "bastion_out_internal" {
  name                        = "AllowBastionCommunication"
  priority                    = 120
  direction                   = "Outbound"
  access                      = "Allow"
  protocol                    = "*" # "Any"
  source_address_prefix       = "VirtualNetwork"
  source_port_range           = "*"
  destination_address_prefix  = "VirtualNetwork"
  destination_port_ranges     = ["8080", "5701"]
  resource_group_name         = azurerm_resource_group.rg.name
  network_security_group_name = azurerm_network_security_group.bastion_subnet_nsg.name
}

resource "azurerm_network_security_rule" "bastion_out_443_azurecloud" {
  name                        = "AllowAzureCloudOutbound"
  priority                    = 110
  direction                   = "Outbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = "*"
  source_port_range           = "*"
  destination_address_prefix  = "AzureCloud"
  destination_port_range      = "443"
  resource_group_name         = azurerm_resource_group.rg.name
  network_security_group_name = azurerm_network_security_group.bastion_subnet_nsg.name
}

resource "azurerm_network_security_rule" "bastion_out_80_internet" {
  name                        = "AllowHttpOutbound"
  priority                    = 130
  direction                   = "Outbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_address_prefix       = "*"
  source_port_range           = "*"
  destination_address_prefix  = "Internet"
  destination_port_range      = "80"
  resource_group_name         = azurerm_resource_group.rg.name
  network_security_group_name = azurerm_network_security_group.bastion_subnet_nsg.name
}

resource "azurerm_subnet_network_security_group_association" "bastion_subnet_nsg_associate" {
  depends_on = [
    azurerm_network_security_rule.bastion_in_443_internet,
    azurerm_network_security_rule.bastion_in_443_gatewaymanager,
    azurerm_network_security_rule.bastion_in_443_azlb,
    azurerm_network_security_rule.bastion_in_internal,
    azurerm_network_security_rule.bastion_out_ssh_rdp,
    azurerm_network_security_rule.bastion_out_443_azurecloud,
    azurerm_network_security_rule.bastion_out_internal,
    azurerm_network_security_rule.bastion_out_80_internet,
  ]
  subnet_id                 = azurerm_subnet.bastion_subnet.id
  network_security_group_id = azurerm_network_security_group.bastion_subnet_nsg.id
}

# NSG
resource "azurerm_network_security_group" "nsg" {
  name                = "${var.project_name}-nsg"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  tags                = azurerm_resource_group.rg.tags

  security_rule {
    name                       = "Allow_HTTP_80"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "80"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "Allow_HTTPS_443"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "Allow_RDP_3389"
    priority                   = 120
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "3389"
    source_address_prefix      = var.rdp_allowed_cidr
    destination_address_prefix = "*"
  }
}

# Public IP for the VM
resource "azurerm_public_ip" "pip" {
  name                = "${var.project_name}-pip"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = azurerm_resource_group.rg.tags
}

# NIC
resource "azurerm_network_interface" "nic" {
  name                = "${var.project_name}-nic"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name

  ip_configuration {
    name                          = "ipconfig1"
    subnet_id                     = azurerm_subnet.subnet.id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.pip.id
  }

  tags = azurerm_resource_group.rg.tags
}

# Associate NSG to NIC (or you can attach to subnet)
resource "azurerm_network_interface_security_group_association" "nic_nsg" {
  depends_on = [ azurerm_network_security_group.nsg ]
  network_interface_id      = azurerm_network_interface.nic.id
  network_security_group_id = azurerm_network_security_group.nsg.id
}


