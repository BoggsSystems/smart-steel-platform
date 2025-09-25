output "namespace_name" {
  value = azurerm_eventhub_namespace.steel_mill.name
}

output "connection_string" {
  value     = azurerm_eventhub_namespace.steel_mill.default_primary_connection_string
  sensitive = true
}

output "telemetry_hub_name" {
  value = azurerm_eventhub.telemetry.name
}

output "ml_predictions_hub_name" {
  value = azurerm_eventhub.ml_predictions.name
}