import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { Search } from '../search/search';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterModule, Search],
  templateUrl: './navbar.html',
  styleUrls: ['./navbar.scss'],
})
export class Navbar {}
