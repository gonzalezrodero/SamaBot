terraform {
  backend "s3" {
    bucket       = "chatbot-tf-state-543704476214"
    key          = "compute/terraform.tfstate"
    region       = "eu-west-1"
    use_lockfile = true
    encrypt      = true
  }

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

# Fetch network state to get VPC and public subnets
data "terraform_remote_state" "network" {
  backend = "s3"
  config = {
    bucket = "chatbot-tf-state-543704476214"
    key    = "network/terraform.tfstate"
    region = "eu-west-1"
  }
}