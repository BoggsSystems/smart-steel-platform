terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "steel_mill" {
  name     = var.resource_group_name
  location = var.location
}

module "iot_hub" {
  source              = "./modules/iot_hub"
  resource_group_name = azurerm_resource_group.steel_mill.name
  location           = azurerm_resource_group.steel_mill.location
  name_prefix        = var.name_prefix
}

module "event_hub" {
  source              = "./modules/event_hub"
  resource_group_name = azurerm_resource_group.steel_mill.name
  location           = azurerm_resource_group.steel_mill.location
  name_prefix        = var.name_prefix
}

module "service_bus" {
  source              = "./modules/service_bus"
  resource_group_name = azurerm_resource_group.steel_mill.name
  location           = azurerm_resource_group.steel_mill.location
  name_prefix        = var.name_prefix
}

module "cosmos_db" {
  source              = "./modules/cosmos_db"
  resource_group_name = azurerm_resource_group.steel_mill.name
  location           = azurerm_resource_group.steel_mill.location
  name_prefix        = var.name_prefix
}

module "storage" {
  source              = "./modules/storage"
  resource_group_name = azurerm_resource_group.steel_mill.name
  location           = azurerm_resource_group.steel_mill.location
  name_prefix        = var.name_prefix
}

module "key_vault" {
  source              = "./modules/key_vault"
  resource_group_name = azurerm_resource_group.steel_mill.name
  location           = azurerm_resource_group.steel_mill.location
  name_prefix        = var.name_prefix
  tenant_id          = data.azurerm_client_config.current.tenant_id
  object_id          = data.azurerm_client_config.current.object_id
}

module "azure_ml" {
  source              = "./modules/azure_ml"
  resource_group_name = azurerm_resource_group.steel_mill.name
  location           = azurerm_resource_group.steel_mill.location
  name_prefix        = var.name_prefix
  storage_account_id = module.storage.storage_account_id
  key_vault_id      = module.key_vault.key_vault_id
}

data "azurerm_client_config" "current" {}