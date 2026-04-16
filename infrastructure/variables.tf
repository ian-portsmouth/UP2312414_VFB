variable "project_name" {
  description = "A short name used to prefix resources."
  type        = string
  default     = "vfb"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "centralus"
}

variable "admin_username" {
  description = "Local admin username for the Windows VM"
  type        = string
  default     = "foodbankadmin"
}

variable "admin_password" {
  description = "Local admin password for the Windows VM"
  type        = string
  # sensitive   = true
  default = "Dellsvcs1!"

}

variable "rdp_allowed_cidr" {
  description = "CIDR allowed to RDP to the VM ."
  type        = string
  default     = "0.0.0.0/0"
}

# SQL
variable "sql_admin_login" {
  description = "SQL admin login name"
  type        = string
  default     = "sqladminuser"
}

variable "sql_admin_password" {
  description = "SQL admin password"
  type        = string
  # sensitive   = true
  default = "Dellsvcs1!"
}

variable "sql_db_name" {
  description = "Azure SQL Database name"
  type        = string
  default     = "FoodBankDB"

}

variable "vnet_prefix" {
  description = "root address space"
  default     = "10.10.0.0/16"

}

variable "snet_prefix" {
  description = "subnet address space"
  default     = "10.10.1.0/24"

}

variable "bastion_prefix" {
  description = "Bastion address range"
  default     = "10.10.2.0/27"

}
