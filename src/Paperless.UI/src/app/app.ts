import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { Navbar } from './components/navbar/navbar';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterModule, Navbar],
  templateUrl: './app.html',
  styleUrls: ['./app.scss'],
})
export class App {}
