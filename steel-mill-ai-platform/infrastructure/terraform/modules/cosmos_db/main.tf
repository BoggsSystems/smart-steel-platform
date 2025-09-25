resource "azurerm_cosmosdb_account" "steel_mill" {
  name                = "${var.name_prefix}-cosmos-${var.location_short}"
  location            = var.location
  resource_group_name = var.resource_group_name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  enable_automatic_failover = true
  enable_free_tier         = false

  consistency_policy {
    consistency_level       = "Session"
    max_interval_in_seconds = 5
    max_staleness_prefix    = 100
  }

  geo_location {
    location          = var.location
    failover_priority = 0
  }

  tags = {
    environment = "production"
    service     = "steel-mill-platform"
  }
}

resource "azurerm_cosmosdb_sql_database" "telemetry" {
  name                = "SteelMillTelemetry"
  resource_group_name = azurerm_cosmosdb_account.steel_mill.resource_group_name
  account_name        = azurerm_cosmosdb_account.steel_mill.name
  throughput          = 400
}

resource "azurerm_cosmosdb_sql_container" "sensor_data" {
  name                  = "SensorData"
  resource_group_name   = azurerm_cosmosdb_account.steel_mill.resource_group_name
  account_name          = azurerm_cosmosdb_account.steel_mill.name
  database_name         = azurerm_cosmosdb_sql_database.telemetry.name
  partition_key_path    = "/deviceId"
  partition_key_version = 1
  throughput            = 400

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }

    excluded_path {
      path = "/\"_etag\"/?"
    }
  }

  unique_key {
    paths = ["/deviceId", "/timestamp"]
  }
}

resource "azurerm_cosmosdb_sql_container" "device_status" {
  name                  = "DeviceStatus"
  resource_group_name   = azurerm_cosmosdb_account.steel_mill.resource_group_name
  account_name          = azurerm_cosmosdb_account.steel_mill.name
  database_name         = azurerm_cosmosdb_sql_database.telemetry.name
  partition_key_path    = "/deviceId"
  partition_key_version = 1
  throughput            = 400
}

resource "azurerm_cosmosdb_sql_container" "ml_predictions" {
  name                  = "MLPredictions"
  resource_group_name   = azurerm_cosmosdb_account.steel_mill.resource_group_name
  account_name          = azurerm_cosmosdb_account.steel_mill.name
  database_name         = azurerm_cosmosdb_sql_database.telemetry.name
  partition_key_path    = "/deviceId"
  partition_key_version = 1
  throughput            = 400
}