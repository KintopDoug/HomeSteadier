-- Conditional database creation script
-- This script only creates the database if it doesn't already exist

SELECT 'CREATE DATABASE homesteadier_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'homesteadier_db')\gexec
