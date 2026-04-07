terraform {
  backend "s3" {
    bucket       = "chatbot-tf-state-543704476214"
    key          = "db/terraform.tfstate"
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

# Pull outputs from the network layer (VPC, Subnets)
data "terraform_remote_state" "network" {
  backend = "s3"
  config = {
    bucket = "chatbot-tf-state-543704476214"
    key    = "network/terraform.tfstate"
    region = "eu-west-1"
  }
}