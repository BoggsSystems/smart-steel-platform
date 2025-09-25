output "iot_hub_connection_string" {
  value     = module.iot_hub.connection_string
  sensitive = true
}

output "event_hub_connection_string" {
  value     = module.event_hub.connection_string
  sensitive = true
}

output "service_bus_connection_string" {
  value     = module.service_bus.connection_string
  sensitive = true
}

output "cosmos_db_endpoint" {
  value = module.cosmos_db.endpoint
}

output "cosmos_db_key" {
  value     = module.cosmos_db.primary_key
  sensitive = true
}

output "storage_account_name" {
  value = module.storage.storage_account_name
}

output "storage_connection_string" {
  value     = module.storage.connection_string
  sensitive = true
}

output "key_vault_uri" {
  value = module.key_vault.vault_uri
}

output "ml_workspace_name" {
  value = module.azure_ml.workspace_name
}