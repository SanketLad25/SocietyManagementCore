
pipeline {
    agent any

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
    }

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = 'true'
    }

    stages {

        stage('Checkout') {
            steps {
                echo 'Checking out backend source code'

                checkout scm

                bat 'git branch --show-current'
            }
        }

        stage('Verify SDK') {
            steps {
                echo 'Checking .NET SDK'

                bat '''
                    dotnet --version
                    dotnet --list-sdks
                '''
            }
        }

        stage('Restore') {
            steps {
                echo 'Restoring NuGet packages'

                bat '''
                    dotnet restore "SocietyManagementCore.sln"
                '''
            }
        }

        stage('Build') {
            steps {
                echo 'Building backend application'

                bat '''
                    dotnet build "SocietyManagementCore.sln" ^
                    --configuration Release ^
                    --no-restore
                '''
            }
        }

        stage('Publish') {
            steps {
                echo 'Publishing backend application'

                bat '''
                    if exist "artifacts\\backend" (
                        rmdir /s /q "artifacts\\backend"
                    )

                    dotnet publish "SocietyManagementCore.csproj" ^
                    --configuration Release ^
                    --no-restore ^
                    --output "artifacts\\backend"
                '''
            }
        }

        stage('Archive Artifact') {
            steps {
                echo 'Archiving published application'

                archiveArtifacts(
                    artifacts: 'artifacts/backend/**',
                    fingerprint: true
                )
            }
        }
    }

    post {
        success {
            echo 'Backend CI Pipeline completed successfully!'
        }

        failure {
            echo 'Backend CI Pipeline failed. Check console output.'
        }
    }
}
