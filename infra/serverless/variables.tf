variable "aws_region" {
  type        = string
  description = "AWS region"
}

variable "project_name" {
  type        = string
  description = "Name of the project"
}

variable "app_environment" {
  type        = string
  description = "Environment (dev, stg, prod)"
}

variable "image_tag" {
  type        = string
  description = "Docker image tag to deploy (usually the Git SHA)"
}

variable "aws_account_id" {
  type        = string
  description = "AWS Account ID"
}

variable "admin_ui_callback_urls" {
  type        = list(string)
  description = "List of allowed callback URLs for the Cognito Hosted UI"
}

variable "admin_ui_logout_urls" {
  type        = list(string)
  description = "List of allowed logout URLs for the Cognito Hosted UI"
}