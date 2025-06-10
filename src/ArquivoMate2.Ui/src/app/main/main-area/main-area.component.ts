import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DocumentsService } from '../../client/services/documents.service';
import { WeatherForecastService } from '../../client/services/weather-forecast.service';

@Component({
  selector: 'app-main-area',
  imports: [RouterOutlet],
  templateUrl: './main-area.component.html',
  styleUrl: './main-area.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainAreaComponent {
  private documentsService = inject(DocumentsService);
  private weatherService = inject(WeatherForecastService);

  public doit() {
    console.log('Button clicked!');
    
    // Test WeatherForecast zuerst (meist ohne Auth)
    console.log('Testing WeatherForecast API...');
    this.weatherService.getWeatherForecast$Json().subscribe({
      next: (data) => {
        console.log('WeatherForecast SUCCESS:', data);
        
        // Dann teste Documents API
        console.log('Testing Documents API...');
        this.documentsService.apiDocumentsGet$Json({ Page: 1, PageSize: 15 }).subscribe({
          next: (documents) => {
            console.log('Documents SUCCESS:', documents);
          },
          error: (error) => {
            console.error('Documents ERROR:', error);
            this.logDetailedError(error);
          }
        });
      },
      error: (error) => {
        console.error('WeatherForecast ERROR:', error);
        this.logDetailedError(error);
      }
    });
  }

  private logDetailedError(error: any) {
    console.error('Detailed error info:', {
      status: error.status,
      statusText: error.statusText,
      url: error.url,
      message: error.message,
      errorBody: error.error,
      headers: error.headers
    });
  }
}
