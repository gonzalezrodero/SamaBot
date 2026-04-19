variable "aws_region" {
  description = "AWS region to deploy to"
  type        = string
}

variable "project_name" {
  description = "Name of the project"
  type        = string
}

variable "app_environment" {
  description = "The environment name for ASP.NET Core"
  type        = string
}

variable "image_tag" {
  description = "The tag of the image to deploy (CalVer)"
  type        = string
  default     = "latest"
}

variable "terraform_state_bucket" {
  description = "Name of the S3 bucket where the bootstrap state is stored"
  type        = string
}