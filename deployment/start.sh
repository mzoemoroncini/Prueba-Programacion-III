#!/bin/sh
set -eu
dotnet Offers.Api.dll --migrate
exec dotnet Offers.Api.dll --urls "http://0.0.0.0:${PORT:-8080}"
