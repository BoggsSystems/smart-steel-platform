variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
  default     = "rg-steel-mill-platform"
}

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "East US"
}

variable "name_prefix" {
  description = "Prefix for resource names"
  type        = string
  default     = "steelmill"
}

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "dev"
}