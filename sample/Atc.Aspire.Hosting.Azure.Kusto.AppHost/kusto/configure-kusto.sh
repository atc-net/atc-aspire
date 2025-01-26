#!/bin/bash

echo "Echo from configure-kusto.sh..."

# Wait for the database to be ready
echo "Waiting for the database to start..."
until curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:8080/v1/rest/query \
    -H "Content-Type: application/json" \
    -d '{"db": "NetDefaultDB","csl": ".show tables"}' | grep -q "200"; do
    echo "Database not ready, retrying in 2 seconds..."
    sleep 2
done

echo "Database is ready!"

# Check if the Todo table already exists
TABLE_EXISTS=$(curl -s -X POST http://localhost:8080/v1/rest/query \
    -H "Content-Type: application/json" \
    -d '{"db": "NetDefaultDB","csl": ".show tables"}' | grep -o "Todo")

if [[ "$TABLE_EXISTS" == "Todo" ]]; then
    echo "Todo table already exists. Skipping creation and data insertion."
else
    echo "Todo table does not exist. Creating table and inserting data..."

    # Create the Todo table
    curl -X POST http://localhost:8080/v1/rest/mgmt \
    -H "Content-Type: application/json" \
    -d '{"db": "NetDefaultDB","csl": ".create table Todo (Id: int, Title: string, Description: string, Status: string, Created: datetime, Priority: string, Closed: datetime)"}'

    # Insert data into the Todo table
    curl -X POST http://localhost:8080/v1/rest/mgmt \
    -H "Content-Type: application/json" \
    -d '{
      "db": "NetDefaultDB",
      "csl": ".ingest inline into table Todo\n    [1, Watch Netflix, Watch the new show, Pending, datetime(2025-01-28T10:00:00Z), Low, null]\n    [2, Make food, Try out the new dish from the Netflix show, Pending, datetime(2025-01-27T15:30:00Z), Medium, null]\n    [3, Coding, Code up the new feature in atc-aspire kusto package, Ended, datetime(2025-01-26T09:15:00Z), High, datetime(2025-01-27T12:00:00Z)]"
    }'

fi
