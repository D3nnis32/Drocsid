#!/bin/bash
set -e

# Wait for PostgreSQL to be ready
echo "Waiting for PostgreSQL..."
until PGPASSWORD=123 psql -h postgres -U postgres -d postgres -c '\q'; do
  >&2 echo "PostgreSQL is unavailable - sleeping"
  sleep 1
done

# Create the database if it doesn't exist
echo "Creating registry database if it doesn't exist..."
PGPASSWORD=123 psql -h postgres -U postgres -tc "SELECT 1 FROM pg_database WHERE datname = 'drocsid_registry'" | grep -q 1 || PGPASSWORD=123 psql -h postgres -U postgres -c "CREATE DATABASE drocsid_registry"

# Apply Entity Framework migrations
echo "Applying database migrations..."
dotnet ef database update --context RegistryDbContext

# Start the registry service
echo "Starting registry service..."
exec dotnet RegistryService.dll