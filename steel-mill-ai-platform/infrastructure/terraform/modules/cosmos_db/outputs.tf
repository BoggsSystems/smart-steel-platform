output "endpoint" {
  value = azurerm_cosmosdb_account.steel_mill.endpoint
}

output "primary_key" {
  value     = azurerm_cosmosdb_account.steel_mill.primary_key
  sensitive = true
}

output "connection_string" {
  value     = azurerm_cosmosdb_account.steel_mill.connection_strings[0]
  sensitive = true
}

output "database_name" {
  value = azurerm_cosmosdb_sql_database.telemetry.name
}