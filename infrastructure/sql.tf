# Azure SQL logical server
resource "azurerm_mssql_server" "sql" {
  name                         = "${var.project_name}-sql-${random_string.suffix.result}"
  resource_group_name          = azurerm_resource_group.rg.name
  location                     = azurerm_resource_group.rg.location
  version                      = "12.0"

  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password

  minimum_tls_version = "1.2"

  tags = azurerm_resource_group.rg.tags
}

# Azure SQL Database
resource "azurerm_mssql_database" "db" {
  name           = var.sql_db_name
  server_id      = azurerm_mssql_server.sql.id
  sku_name       = "S0"          
  zone_redundant = false


  tags = azurerm_resource_group.rg.tags
}


resource "azurerm_mssql_virtual_network_rule" "sql_vnet_rule" {
  name      = "${var.project_name}-sql-vnetrule"
  server_id = azurerm_mssql_server.sql.id
  subnet_id = azurerm_subnet.subnet.id
}
#