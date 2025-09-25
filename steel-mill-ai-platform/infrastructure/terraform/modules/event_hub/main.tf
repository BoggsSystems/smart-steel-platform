resource "azurerm_eventhub_namespace" "steel_mill" {
  name                = "${var.name_prefix}-ehns-${var.location_short}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "Standard"
  capacity            = 2

  tags = {
    environment = "production"
    service     = "steel-mill-platform"
  }
}

resource "azurerm_eventhub" "telemetry" {
  name                = "steel-mill-telemetry"
  namespace_name      = azurerm_eventhub_namespace.steel_mill.name
  resource_group_name = var.resource_group_name
  partition_count     = 4
  message_retention   = 7
}

resource "azurerm_eventhub" "ml_predictions" {
  name                = "ml-predictions"
  namespace_name      = azurerm_eventhub_namespace.steel_mill.name
  resource_group_name = var.resource_group_name
  partition_count     = 2
  message_retention   = 1
}

resource "azurerm_eventhub_consumer_group" "microservices" {
  for_each = toset(["furnace-service", "rolling-mill-service", "ml-inference"])
  
  name                = each.value
  namespace_name      = azurerm_eventhub_namespace.steel_mill.name
  eventhub_name       = azurerm_eventhub.telemetry.name
  resource_group_name = var.resource_group_name
}

resource "azurerm_eventhub_authorization_rule" "send" {
  name                = "send-rule"
  namespace_name      = azurerm_eventhub_namespace.steel_mill.name
  eventhub_name       = azurerm_eventhub.telemetry.name
  resource_group_name = var.resource_group_name
  listen              = false
  send                = true
  manage              = false
}

resource "azurerm_eventhub_authorization_rule" "listen" {
  name                = "listen-rule"
  namespace_name      = azurerm_eventhub_namespace.steel_mill.name
  eventhub_name       = azurerm_eventhub.telemetry.name
  resource_group_name = var.resource_group_name
  listen              = true
  send                = false
  manage              = false
}